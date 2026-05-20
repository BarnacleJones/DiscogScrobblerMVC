using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using DiscogScrobblerMVC.Data;
using DiscogScrobblerMVC.Models;
using DiscogScrobblerMVC.Services.Interfaces;
using DiscogScrobblerMVC.Services.Utilities;
using Hqub.Lastfm;
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

    /// <summary>Discogs artist names may end with a numeric disambiguator such as "(2)".</summary>
    private static readonly Regex DiscogsArtistDisambiguationSuffix =
        new(@"\s*\(\d+\)\s*$", RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(250));

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
                Artists = x.Artists.Select(y => new { y.Id, y.Name, y.LastFmArtistName }).ToList(),
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
            .OrderBy(x => x.Position, TrackPositionComparer.Instance)
            .Where(y => !string.IsNullOrWhiteSpace(y.Title))
            .ToList();

        if (tracks.Count == 0)
            return ScrobbleFailureReason.NoTracks;

        var apiKey = _options.ApiKey.Trim();
        var apiSecret = _options.ApiSecret.Trim();

        var lastFmClient = new LastfmClient(apiKey, apiSecret);
        var releaseArtists = release.Artists
            .Select(x => new ReleaseArtistForScrobble(x.Id, x.Name, x.LastFmArtistName))
            .ToList();
        var resolvedByArtistId =
            await ResolveAndPersistArtistNamesForScrobbleAsync(lastFmClient, releaseArtists, cancellationToken);
        var resolvedArtistNames = releaseArtists.ConvertAll(x => resolvedByArtistId[x.Id]);
        // Multi-artist releases: resolve the joined collaboration name via Last.fm on each scrobble.
        // Intentionally not persisted — Artist.LastFmArtistName is solo-artist only.
        var artist = await ResolveCombinedAlbumArtistForScrobbleAsync(
            lastFmClient,
            resolvedArtistNames,
            cancellationToken);

        var albumRaw = release.Album?.Trim();
        var album =
            string.IsNullOrWhiteSpace(albumRaw) ? null : albumRaw;

        var albumEndUtc = DateTime.UtcNow;
        var rowArtist = NormalizeScrobbleText(artist);
        if (string.IsNullOrEmpty(rowArtist))
            rowArtist = "Unknown Artist";

        var parsedDurations = tracks.ConvertAll(x => x.DurationSeconds ?? TrackDurationParser.TryParseSeconds(x.Duration));
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

    private bool HasApiKeyPair()
    {
        return !string.IsNullOrWhiteSpace(_options.ApiKey)
            && !string.IsNullOrWhiteSpace(_options.ApiSecret);
    }

    private static List<string> DistinctOrderedArtistNames(IEnumerable<string> artists)
    {
        return artists
            .Select(x => x.Trim())
            .Where(y => y.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(y => y, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string FormatAlbumArtistCommaJoined(IReadOnlyList<string> orderedDistinctArtists)
    {
        if (orderedDistinctArtists.Count == 0)
            return "Unknown Artist";

        return string.Join(", ", orderedDistinctArtists);
    }

    private static string FormatAlbumArtistAmpersandJoined(IReadOnlyList<string> orderedDistinctArtists)
    {
        if (orderedDistinctArtists.Count == 0)
            return "Unknown Artist";

        return string.Join(" & ", orderedDistinctArtists);
    }

    /// <summary>
    /// Builds the album-level artist string for <c>track.scrobble</c>.
    /// Solo names come from persisted per-artist resolution; when 2+ distinct artists are credited,
    /// the joined collaboration name is resolved via Last.fm on each scrobble and is not stored in the database.
    /// </summary>
    private async Task<string> ResolveCombinedAlbumArtistForScrobbleAsync(
        LastfmClient client,
        IEnumerable<string> resolvedArtistNames,
        CancellationToken cancellationToken)
    {
        var distinct = DistinctOrderedArtistNames(resolvedArtistNames);
        if (distinct.Count == 0)
            return "Unknown Artist";
        if (distinct.Count == 1)
            return distinct[0];

        cancellationToken.ThrowIfCancellationRequested();

        var commaJoined = FormatAlbumArtistCommaJoined(distinct);
        var correctedFromComma = await TryGetLastFmArtistCorrectionNameAsync(client, commaJoined);
        if (!string.IsNullOrEmpty(correctedFromComma)
            && !string.Equals(correctedFromComma, commaJoined, StringComparison.OrdinalIgnoreCase))
        {
            var resolved = NormalizeScrobbleText(correctedFromComma);
            _logger.LogInformation(
                "Last.fm album artist for scrobble: \"{Joined}\" -> \"{Resolved}\".",
                commaJoined,
                resolved);
            return resolved;
        }

        var ampersandJoined = FormatAlbumArtistAmpersandJoined(distinct);
        var correctedFromAmpersand = await TryGetLastFmArtistCorrectionNameAsync(client, ampersandJoined);
        if (!string.IsNullOrEmpty(correctedFromAmpersand)
            && !string.Equals(correctedFromAmpersand, ampersandJoined, StringComparison.OrdinalIgnoreCase))
        {
            var resolved = NormalizeScrobbleText(correctedFromAmpersand);
            _logger.LogInformation(
                "Last.fm album artist for scrobble: \"{Joined}\" -> \"{Resolved}\".",
                ampersandJoined,
                resolved);
            return resolved;
        }

        return NormalizeScrobbleText(ampersandJoined);
    }

    private readonly record struct ReleaseArtistForScrobble(int Id, string Name, string? LastFmArtistName);

    /// <summary>
    /// Builds a map of artist Id → normalized scrobble artist name.
    /// Uses <see cref="Artist.LastFmArtistName"/> when set; otherwise resolves via Last.fm once and persists it.</summary>
    private async Task<IReadOnlyDictionary<int, string>> ResolveAndPersistArtistNamesForScrobbleAsync(
        LastfmClient client,
        IReadOnlyList<ReleaseArtistForScrobble> releaseArtists,
        CancellationToken cancellationToken)
    {
        var primaryRowById = releaseArtists
            .GroupBy(x => x.Id)
            .ToDictionary(x => x.Key, x => x.First());

        var result = new Dictionary<int, string>();
        foreach (var id in primaryRowById.Keys)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var row = primaryRowById[id];
            var stored = row.LastFmArtistName?.Trim();
            if (!string.IsNullOrEmpty(stored))
            {
                var fromStore = NormalizeScrobbleText(stored);
                result[id] = string.IsNullOrEmpty(fromStore) ? NormalizeScrobbleText(row.Name) : fromStore;
                continue;
            }

            var computed = await ComputeResolvedArtistNameForScrobbleAsync(client, row.Name);
            result[id] = computed;

            await _db.Artists
                .Where(x => x.Id == id)
                .ExecuteUpdateAsync(
                    x => x.SetProperty(a => a.LastFmArtistName, computed),
                    cancellationToken);

            var normalizedRaw = NormalizeScrobbleText(row.Name);
            if (computed != normalizedRaw)
            {
                _logger.LogInformation(
                    "Persisted Last.fm scrobble artist for artist Id {ArtistId}: \"{Raw}\" -> \"{Resolved}\".",
                    id,
                    row.Name,
                    computed);
            }
        }

        return result;
    }

    private async Task<string> ComputeResolvedArtistNameForScrobbleAsync(LastfmClient client, string rawName)
    {
        var corrected = await TryGetLastFmArtistCorrectionNameAsync(client, rawName);

        string chosen;
        if (!string.IsNullOrEmpty(corrected) && !string.Equals(corrected, rawName, StringComparison.Ordinal))
            chosen = corrected;
        else
            chosen = StripDiscogsArtistDisambiguationSuffix(rawName);

        var normalizedChosen = NormalizeScrobbleText(chosen);
        if (string.IsNullOrEmpty(normalizedChosen))
            normalizedChosen = NormalizeScrobbleText(rawName);

        return normalizedChosen;
    }

    /// <summary>
    /// Returns Last.fm's corrected artist name, or null when the API has no match (including invalid combined names).
    /// </summary>
    private async Task<string?> TryGetLastFmArtistCorrectionNameAsync(LastfmClient client, string rawName)
    {
        try
        {
            var entity = await client.Artist.GetCorrectionAsync(rawName);
            return entity?.Name?.Trim();
        }
        catch (ServiceException ex)
        {
            // Last.fm throws when the supplied name is not a known artist (common for comma-joined collaborations).
            _logger.LogDebug(ex, "Last.fm artist.getCorrection had no match for \"{Artist}\".", rawName);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Last.fm artist.getCorrection failed for \"{Artist}\".", rawName);
            return null;
        }
    }

    private static string StripDiscogsArtistDisambiguationSuffix(string name)
    {
        var trimmedOuter = name.Trim();
        if (trimmedOuter.Length == 0)
            return name;

        return DiscogsArtistDisambiguationSuffix.Replace(trimmedOuter, "").TrimEnd();
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
        var knownDurations = parsedSecondsPerTrack.Where(x => x is > 0).Select(y => y!.Value).ToList();
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

        using var response = await http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        var doc = XDocument.Parse(body);
        var lfm = doc.Root;
        var status = lfm?.Attribute("status")?.Value;
        if (status == "ok")
            return;

        var errEl = lfm?.Elements().FirstOrDefault(x => x.Name.LocalName == "error");
        var code = errEl?.Attribute("code")?.Value;
        var msg = errEl?.Value?.Trim();
        throw new InvalidOperationException(
            string.IsNullOrEmpty(msg)
                ? $"Last.fm track.scrobble failed (HTTP {(int)response.StatusCode}, code {code ?? "?"}): {body}"
                : $"Last.fm track.scrobble failed (code {code ?? "?"}): {msg}");
    }
}
