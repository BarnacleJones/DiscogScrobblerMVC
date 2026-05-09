using DiscogScrobblerMVC.Data;
using DiscogScrobblerMVC.Models;
using DiscogScrobblerMVC.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DiscogScrobblerMVC.Services;

public class StatsService : IStatsService
{
    /// <summary>
    /// Discogs places compilations and "Various Artists" releases under artist id 194.
    /// We surface those releases as a footnote under the Top Artists chart instead of
    /// letting them dominate the bar.
    /// </summary>
    private const int VariousDiscogsArtistId = 194;

    private const int TopChartEntryCount = 10;

    private readonly ApplicationDbContext _db;
    private readonly IDiscogsSyncQueue _syncQueue;
    private readonly ILogger<StatsService> _logger;

    public StatsService(
        ApplicationDbContext db,
        IDiscogsSyncQueue syncQueue,
        ILogger<StatsService> logger)
    {
        _db = db;
        _syncQueue = syncQueue;
        _logger = logger;
    }

    public async Task<StatsViewModel> GetStatsAsync(string userId, CancellationToken cancellationToken = default)
    {
        // IMPORTANT: avoid `Contains(subquery)` patterns here. EF Core + SQLite can fail
        // to translate some grouped/joined shapes when the predicate is `IN (SELECT ...)`.
        // Filtering via navigations (`...Release.UserAssociations.Any(...)`) stays fully
        // translatable and uses our indexes.
        var ownedReleases = _db.Releases.AsNoTracking()
            .Where(x => x.UserAssociations.Any(y => y.UserId == userId));

        var ownedReleaseCount = await ownedReleases.CountAsync(cancellationToken);

        var viewModel = new StatsViewModel
        {
            HasAnyReleases = ownedReleaseCount > 0,
            OwnedReleaseCount = ownedReleaseCount,
        };

        if (!viewModel.HasAnyReleases)
            return viewModel;

        // (1) Average track length + sample size + total tracks. GroupBy(_=>1) on an empty
        // source yields zero rows, so FirstOrDefaultAsync returns null — coalesce in C#.
        // SQL AVG already ignores nulls, so we don't filter inside Average().
        var trackAggregate = await _db.Tracks.AsNoTracking()
            .Where(x => x.Release.UserAssociations.Any(y => y.UserId == userId))
            .GroupBy(_ => 1)
            .Select(x => new
            {
                Avg = x.Average(y => (double?)y.DurationSeconds),
                Sample = x.Count(y => y.DurationSeconds != null),
                Total = x.Count(),
            })
            .FirstOrDefaultAsync(cancellationToken);

        viewModel.AverageTrackSeconds = trackAggregate?.Avg;
        viewModel.TrackDurationSampleSize = trackAggregate?.Sample ?? 0;
        viewModel.TotalTracks = trackAggregate?.Total ?? 0;

        // Releases with at least one track ingested — drives the "syncing N…" hint.
        viewModel.IngestedReleaseCount = await _db.Tracks.AsNoTracking()
            .Where(x => x.Release.UserAssociations.Any(y => y.UserId == userId))
            .Select(x => x.ReleaseId)
            .Distinct()
            .CountAsync(cancellationToken);

        // (2) Avg tracks per release: denominator is owned releases per product decision;
        // sync layer keeps it honest. Guard against zero here even though we returned early.
        viewModel.AverageTracksPerRelease = ownedReleaseCount == 0
            ? null
            : (double)viewModel.TotalTracks / ownedReleaseCount;

        // (3) Letter distribution — count distinct artists per first-letter bucket.
        // Trim() server-side handles leading whitespace; SQLite upper() is ASCII-only so
        // any non A–Z first letter (Björk, "50 Cent", "...And You Will Know Us") folds
        // into '#' in the C# step below. "Various" is intentionally kept here.
        var rawLetterBuckets = await _db.Artists.AsNoTracking()
            .Where(x => x.Releases.Any(y => y.UserAssociations.Any(z => z.UserId == userId)))
            .GroupBy(x => x.Name.Trim().Substring(0, 1).ToUpper())
            .Select(x => new { Letter = x.Key, Count = x.Count() })
            .ToListAsync(cancellationToken);

        viewModel.LetterDistribution = FoldLetterBuckets(rawLetterBuckets.Select(x => (x.Letter, x.Count)));

        // (4) Top styles / top genres — grouping by Name is safe under the existing
        // NormalizedName upsert invariant (see DiscogsService ~line 700). ThenBy(Name)
        // gives stable ordering on ties.
        var topStylesRaw = await _db.ReleaseStyles.AsNoTracking()
            .Where(x => x.Release.UserAssociations.Any(y => y.UserId == userId))
            .GroupBy(x => x.Style.Name)
            .Select(x => new { Name = x.Key, Count = x.Count() })
            .OrderByDescending(x => x.Count).ThenBy(x => x.Name)
            .Take(TopChartEntryCount)
            .ToListAsync(cancellationToken);

        viewModel.TopStyles = topStylesRaw.Select(x => new NameCountViewModel(x.Name, x.Count)).ToList();

        var topGenresRaw = await _db.ReleaseGenres.AsNoTracking()
            .Where(x => x.Release.UserAssociations.Any(y => y.UserId == userId))
            .GroupBy(x => x.Genre.Name)
            .Select(x => new { Name = x.Key, Count = x.Count() })
            .OrderByDescending(x => x.Count).ThenBy(x => x.Name)
            .Take(TopChartEntryCount)
            .ToListAsync(cancellationToken);

        viewModel.TopGenres = topGenresRaw.Select(x => new NameCountViewModel(x.Name, x.Count)).ToList();

        // (5a) Top artists — single COUNT correlated subquery, Various excluded.
        // Multi-artist releases count once per credited artist (matches DiscogsService.ResolveArtists).
        viewModel.TopArtists = (await _db.Artists.AsNoTracking()
            .Where(x => x.DiscogsArtistId != VariousDiscogsArtistId && x.Name != "Various")
            .Select(x => new
            {
                x.Id,
                x.Name,
                Count = x.Releases.Count(y => y.UserAssociations.Any(z => z.UserId == userId)),
            })
            .Where(x => x.Count > 0)
            .OrderByDescending(x => x.Count).ThenBy(x => x.Name)
            .Take(TopChartEntryCount)
            .ToListAsync(cancellationToken))
            .Select(x => new TopArtistViewModel(x.Id, x.Name, x.Count))
            .ToList();

        // (5b) Footnote: how many releases are credited to the Various placeholder.
        viewModel.VariousReleaseCount = await _db.Artists.AsNoTracking()
            .Where(x => x.DiscogsArtistId == VariousDiscogsArtistId || x.Name == "Various")
            .Select(x => x.Releases.Count(y => y.UserAssociations.Any(z => z.UserId == userId)))
            .SumAsync(cancellationToken);

        if (viewModel.PendingReleaseCount > 0)
        {
            var enqueued = _syncQueue.EnqueueUserFullSync(userId);
            if (enqueued)
            {
                _logger.LogInformation(
                    "Stats page enqueued sync for user {UserId} ({Pending} releases missing tracks)",
                    userId,
                    viewModel.PendingReleaseCount);
            }
        }

        return viewModel;
    }

    /// <summary>
    /// Folds raw SQLite ASCII-uppercased first-letter buckets into A - Z plus '#'.
    /// Any non A–Z key (empty/null, digits, punctuation, non-ASCII letters) goes into '#'.
    /// Always returns 27 buckets in alphabetical order with '#' last so the chart axis is stable.
    /// </summary>
    private static IReadOnlyList<LetterBucketViewModel> FoldLetterBuckets(IEnumerable<(string Letter, int Count)> rawBuckets)
    {
        var byLetter = new Dictionary<char, int>(27);
        foreach (var bucket in rawBuckets)
        {
            var c = string.IsNullOrEmpty(bucket.Letter) ? '#' : char.ToUpperInvariant(bucket.Letter[0]);
            var chartBucket = c is >= 'A' and <= 'Z' ? c : '#';
            byLetter[chartBucket] = byLetter.GetValueOrDefault(chartBucket) + bucket.Count;
        }

        return Enumerable.Range(0, 26)
            .Select(x => (char)('A' + x)).Append('#')
            .Select(x => new LetterBucketViewModel(x, byLetter.GetValueOrDefault(x)))
            .ToList();
    }
}
