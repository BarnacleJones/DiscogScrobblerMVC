using System.Globalization;
using System.Text;
using System.Xml.Linq;
using DiscogScrobblerMVC.Data;
using DiscogScrobblerMVC.Models;
using DiscogScrobblerMVC.Services.Interfaces;
using DiscogScrobblerMVC.Services.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DiscogScrobblerMVC.Services.LastFm;

public class ScrobbleService : IScrobbleService
{
    private const int LastFmScrobbleBatchSize = 50;
    private const string LastFmApiRoot = "https://ws.audioscrobbler.com/2.0/";
    /// <summary>
    /// When Discogs track lengths are missing, assume each unresolved track lasted this many seconds.
    /// </summary>
    private const int PlaybackDurationFallbackSeconds = 180;
    /// <summary>
    /// When some tracks have lengths, missing tracks use max(this, rounded average known length).
    /// </summary>
    private const int MinimumInferredPlaybackSeconds = 60;

    private readonly ApplicationDbContext _db;
    private readonly LastFmOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ScrobbleService> _logger;

    public ScrobbleService(
        ApplicationDbContext db,
        IOptions<LastFmOptions> options,
        IHttpClientFactory httpClientFactory,
        ILogger<ScrobbleService> logger)
    {
        _db = db;
        _options = options.Value;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<ScrobbleFailureReason> ScrobbleReleaseForUserAsync(
        string userId,
        int discogsReleaseId,
        CancellationToken cancellationToken = default)
    {
        if (!HasApiKeyPair())
            return ScrobbleFailureReason.LastFmNotConfigured;

        var sessionKeyResolved = await _db.Users.AsNoTracking()
            .Where(x => x.Id == userId)
            .Select(x => x.LastFmSessionKey)
            .FirstOrDefaultAsync(cancellationToken);
        sessionKeyResolved = sessionKeyResolved?.Trim();
        if (string.IsNullOrEmpty(sessionKeyResolved))
        {
            _logger.LogWarning(
                "Last.fm writes blocked — API key present but this user has no linked Last.fm session. Open /Settings and connect Last.fm.");
            return ScrobbleFailureReason.LastFmNotConfigured;
        }

        var inCollection = await _db.DiscogsReleaseToUsers.AsNoTracking()
            .AnyAsync(x => x.UserId == userId && x.DiscogsReleaseId == discogsReleaseId, cancellationToken);

        if (!inCollection)
            return ScrobbleFailureReason.NotInUserCollection;

        var release = await _db.Releases.AsNoTracking()
            .Where(x => x.DiscogsReleaseId == discogsReleaseId)
            .Select(x => new
            {
                x.Album,
                Artists = x.Artists.Select(y => y.Name).ToList(),
                Tracks = x.Tracks
                    .Where(y => y.Title != "")
                    .Select(y => new
                    {
                        y.Position,
                        y.Title,
                        y.Duration,
                        y.DurationSeconds,
                    })
                    .ToList(),
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (release is null)
            return ScrobbleFailureReason.ReleaseNotFound;

        var tracks = release.Tracks
            .OrderBy(t => t.Position, TrackPositionComparer.Instance)
            .Where(x => !string.IsNullOrWhiteSpace(x.Title))
            .ToList();

        if (tracks.Count == 0)
            return ScrobbleFailureReason.NoTracks;

        var artist = FormatAlbumArtist(release.Artists);
        var albumRaw = release.Album?.Trim();
        var album =
            string.IsNullOrWhiteSpace(albumRaw) ? null : albumRaw;

        var apiKey = _options.ApiKey.Trim();
        var apiSecret = _options.ApiSecret.Trim();

        var albumEndUtc = DateTime.UtcNow;
        var rowArtist = NormalizeScrobbleText(artist);
        if (string.IsNullOrEmpty(rowArtist))
            rowArtist = "Unknown Artist";

        var parsedDurations = tracks.ConvertAll(t => t.DurationSeconds ?? TrackDurationParser.TryParseSeconds(t.Duration));
        var playbackSeconds = ResolvePlaybackDurationsSeconds(parsedDurations);
        var trackStartTimesUtc = ComputeTrackStartTimesUtcAssumingJustFinishedAlbum(albumEndUtc, playbackSeconds);

        var scrobbles = new List<ScrobbleRow>(tracks.Count);
        for (var i = 0; i < tracks.Count; i++)
        {
            var t = tracks[i];
            var trackTitle = NormalizeScrobbleText(t.Title);
            var ts = UnixTimestampUtc(trackStartTimesUtc[i]);
            var durationSec = playbackSeconds[i];
            scrobbles.Add(new ScrobbleRow(rowArtist, trackTitle, album, ts, durationSec));
        }

        try
        {
            var http = _httpClientFactory.CreateClient();
            for (var offset = 0; offset < scrobbles.Count; offset += LastFmScrobbleBatchSize)
            {
                var batch = scrobbles.Skip(offset).Take(LastFmScrobbleBatchSize).ToList();
                await PostTrackScrobbleBatchAsync(http, apiKey, apiSecret, sessionKeyResolved, batch, cancellationToken);
            }

            _logger.LogInformation(
                "Scrobbled {Count} tracks for Discogs release {ReleaseId} ({Artist} — {Album})",
                scrobbles.Count, discogsReleaseId, artist, album);
            return ScrobbleFailureReason.None;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Last.fm scrobble failed for Discogs release {ReleaseId}", discogsReleaseId);
            return ScrobbleFailureReason.LastFmRejected;
        }
    }

    private bool HasApiKeyPair() =>
        !string.IsNullOrWhiteSpace(_options.ApiKey) && !string.IsNullOrWhiteSpace(_options.ApiSecret);

    private static string FormatAlbumArtist(IEnumerable<string> artists)
    {
        var ordered = artists
            .Select(name => name.Trim())
            .Where(x => x.Length > 0)
            .OrderBy(name => name)
            .ToList();

        if (ordered.Count == 0)
            return "Unknown Artist";
        return string.Join(", ", ordered);
    }

    /// <summary>
    /// Model: the user scrobbles at <paramref name="albumEndUtc"/> as if the last track has just finished.
    /// Each track's Last.fm timestamp is its virtual start time (UTC), spaced by resolved track lengths.
    /// </summary>
    private static DateTime[] ComputeTrackStartTimesUtcAssumingJustFinishedAlbum(
        DateTime albumEndUtc,
        IReadOnlyList<int> playbackSecondsPerTrackInOrder)
    {
        var trackCount = playbackSecondsPerTrackInOrder.Count;
        var trackStartTimesUtc = new DateTime[trackCount];
        var nextTrackEndUtc = albumEndUtc;

        for (var i = trackCount - 1; i >= 0; i--)
        {
            var trackLengthSeconds = playbackSecondsPerTrackInOrder[i];
            nextTrackEndUtc = nextTrackEndUtc.AddSeconds(-trackLengthSeconds);
            trackStartTimesUtc[i] = nextTrackEndUtc;
        }

        return trackStartTimesUtc;
    }

    /// <remarks>
    /// Uses Discogs durations when present. Otherwise uses the average known length on the album
    /// (floored by <see cref="MinimumInferredPlaybackSeconds"/>), or
    /// <see cref="PlaybackDurationFallbackSeconds"/> when nothing is known.
    /// </remarks>
    private static int[] ResolvePlaybackDurationsSeconds(IReadOnlyList<int?> parsedSecondsPerTrack)
    {
        var knownDurations = parsedSecondsPerTrack.Where(x => x is > 0).Select(x => x!.Value).ToList();
        var inferredDuration =
            knownDurations.Count > 0
                ? Math.Max(MinimumInferredPlaybackSeconds, (int)Math.Round(knownDurations.Average()))
                : PlaybackDurationFallbackSeconds;

        var resolvedDurations = new int[parsedSecondsPerTrack.Count];
        for (var i = 0; i < parsedSecondsPerTrack.Count; i++)
        {
            var parsedDuration = parsedSecondsPerTrack[i];
            resolvedDurations[i] = parsedDuration is > 0 ? parsedDuration.Value : inferredDuration;
        }

        return resolvedDurations;
    }

    private readonly record struct ScrobbleRow(
        string Artist,
        string Track,
        string? Album,
        long TimestampUtc,
        int? DurationSeconds);

    private static string NormalizeScrobbleText(string? s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return string.Empty;

        return string.Join(
            ' ',
            s.Trim().Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries));
    }

    private static long UnixTimestampUtc(DateTime dateTime)
    {
        var utc = dateTime.Kind switch
        {
            DateTimeKind.Utc => dateTime,
            DateTimeKind.Local => dateTime.ToUniversalTime(),
            _ => DateTime.SpecifyKind(dateTime, DateTimeKind.Utc),
        };

        var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        return (long)Math.Floor((utc - epoch).TotalSeconds);
    }

    private static string SignLastFmParams(SortedDictionary<string, string> unsignedParams, string apiSecret)
    {
        var sb = new StringBuilder();
        foreach (var kv in unsignedParams)
        {
            sb.Append(kv.Key);
            sb.Append(kv.Value);
        }

        sb.Append(apiSecret);
        return Hqub.Lastfm.MD5.ComputeHash(sb.ToString());
    }

    private static async Task PostTrackScrobbleBatchAsync(
        HttpClient http,
        string apiKey,
        string apiSecret,
        string sessionKey,
        IReadOnlyList<ScrobbleRow> batch,
        CancellationToken cancellationToken)
    {
        if (batch.Count == 0)
            return;

        var unsigned = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["method"] = "track.scrobble",
            ["api_key"] = apiKey,
            ["sk"] = sessionKey,
        };

        for (var i = 0; i < batch.Count; i++)
        {
            var s = batch[i];
            var bracket = "[" + i.ToString(CultureInfo.InvariantCulture) + "]";
            unsigned["artist" + bracket] = s.Artist;
            unsigned["track" + bracket] = s.Track;
            unsigned["timestamp" + bracket] = s.TimestampUtc.ToString(CultureInfo.InvariantCulture);

            if (!string.IsNullOrEmpty(s.Album))
                unsigned["album" + bracket] = s.Album!;

            if (s.DurationSeconds is { } d && d > 0)
                unsigned["duration" + bracket] = d.ToString(CultureInfo.InvariantCulture);
        }

        var apiSig = SignLastFmParams(unsigned, apiSecret);

        using var request = new HttpRequestMessage(HttpMethod.Post, LastFmApiRoot)
        {
            Content = new FormUrlEncodedContent(
                unsigned.Select(x => new KeyValuePair<string?, string?>(x.Key, x.Value))
                    .Append(new KeyValuePair<string?, string?>("api_sig", apiSig))),
        };

        request.Headers.TryAddWithoutValidation("User-Agent", "DiscogScrobblerMVC");

        using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        var doc = XDocument.Parse(body);
        var lfm = doc.Root;
        var status = lfm?.Attribute("status")?.Value;
        if (status == "ok")
            return;

        var errEl = lfm?.Elements().FirstOrDefault(e => e.Name.LocalName == "error");
        var code = errEl?.Attribute("code")?.Value;
        var msg = errEl?.Value?.Trim();
        throw new InvalidOperationException(
            string.IsNullOrEmpty(msg)
                ? $"Last.fm track.scrobble failed (HTTP {(int)response.StatusCode}, code {code ?? "?"}): {body}"
                : $"Last.fm track.scrobble failed (code {code ?? "?"}): {msg}");
    }
}
