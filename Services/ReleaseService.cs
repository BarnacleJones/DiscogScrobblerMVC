using DiscogsApiClient;
using DiscogScrobblerMVC.Data;
using DiscogScrobblerMVC.Data.Entities;
using DiscogScrobblerMVC.Models;
using DiscogScrobblerMVC.Services.Interfaces;
using DiscogScrobblerMVC.Services.Utilities;
using Microsoft.EntityFrameworkCore;

namespace DiscogScrobblerMVC.Services;

public class ReleaseService : IReleaseService
{
    private readonly ApplicationDbContext _db;
    private readonly IDiscogsApiClient _discogsApiClient;
    private readonly ILogger<ReleaseService> _logger;

    public ReleaseService(ApplicationDbContext db, IDiscogsApiClient discogsApiClient, ILogger<ReleaseService> logger)
    {
        _db = db;
        _discogsApiClient = discogsApiClient;
        _logger = logger;
    }

    public async Task<ReleaseViewModel?> GetRelease(
        int discogsReleaseId,
        string viewerApplicationUserId,
        CancellationToken cancellationToken = default)
    {
        var discogsCoverSubfolderName =
            await DiscogsCoverSubfolder.TryGetNameForSignedInUserAsync(
                _db, viewerApplicationUserId, cancellationToken);

        var release = await _db.Releases.AsNoTracking()
            .Where(x => x.DiscogsReleaseId == discogsReleaseId)
            .Select(x => new
            {
                ReleaseId = x.DiscogsReleaseId,
                x.SchemaVersion,
                x.DiscogsMasterId,
                x.Album,
                x.Year,
                x.CommunityHaveCount,
                x.CommunityWantCount,
                x.Images!.LocalImageFilename,
                x.Images!.CoverUrl,
                Artists = x.Artists
                    .Select(y => new ArtistLinkViewModel(y.Id, y.Name))
                    .ToList(),
                Labels = x.Labels
                    .Select(y => new LabelLinkViewModel(y.Id, y.Name))
                    .ToList(),
                Genres = x.GenreLinks
                    .Select(y => new GenreLinkViewModel(y.GenreId, y.Genre.Name))
                    .ToList(),
                Styles = x.StyleLinks
                    .Select(y => new StyleLinkViewModel(y.StyleId, y.Style.Name))
                    .ToList(),
                x.Format,
                x.Notes,
                Tracklist = x.Tracks
                    .Select(y => new TrackViewModel { Position = y.Position, Title = y.Title, Duration = y.Duration })
                    .ToList(),
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (release is null)
            return null;

        int usersOwningRelease;
        int usersWantingRelease;
        if (release.SchemaVersion >= Release.ReleaseSchemaVersion)
        {
            usersOwningRelease = release.CommunityHaveCount ?? 0;
            usersWantingRelease = release.CommunityWantCount ?? 0;
        }
        else if (release.CommunityHaveCount is not null && release.CommunityWantCount is not null)
        {
            usersOwningRelease = release.CommunityHaveCount.Value;
            usersWantingRelease = release.CommunityWantCount.Value;
        }
        else
        {
            usersOwningRelease = 0;
            usersWantingRelease = 0;
            try
            {
                var community = await _discogsApiClient.GetRelease(discogsReleaseId, cancellationToken);
                usersOwningRelease = community.CommunityStatistics?.UsersOwningReleaseCount ?? 0;
                usersWantingRelease = community.CommunityStatistics?.UsersWantingReleaseCount ?? 0;

                await _db.Releases
                    .Where(x => x.DiscogsReleaseId == discogsReleaseId)
                    .ExecuteUpdateAsync(
                        s => s
                            .SetProperty(x => x.CommunityHaveCount, usersOwningRelease)
                            .SetProperty(x => x.CommunityWantCount, usersWantingRelease),
                        cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch community stats for release {Id}", discogsReleaseId);
            }
        }

        return new ReleaseViewModel
        {
            ReleaseId = release.ReleaseId,
            DiscogsMasterId = release.DiscogsMasterId,
            Album = release.Album,
            Year = release.Year,
            CoverUrl = CoverImageUrlResolver.ResolveReleaseCoverForHero(
                discogsCoverSubfolderName,
                release.LocalImageFilename,
                release.CoverUrl),
            Have = usersOwningRelease,
            Want = usersWantingRelease,
            Artists = release.Artists,
            Labels = release.Labels,
            Genres = release.Genres
                .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            Styles = release.Styles
                .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            Format = release.Format,
            Notes = release.Notes,
            Tracklist = release.Tracklist
                .OrderBy(x => x.Position, TrackPositionComparer.Instance)
                .ToList(),
        };
    }

    public async Task<ReleaseViewModel?> GetRandomReleaseForUser(string userId, CancellationToken cancellationToken = default)
    {
        var userReleaseIds = _db.DiscogsReleaseToUsers.AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => x.DiscogsReleaseId)
            .Distinct();

        var releaseCount = await userReleaseIds.CountAsync(cancellationToken);
        if (releaseCount == 0)
            return null;

        var randomOffset = Random.Shared.Next(releaseCount);
        var randomReleaseId = await userReleaseIds
            .OrderBy(x => x)
            .Skip(randomOffset)
            .Select(x => (int?)x)
            .FirstOrDefaultAsync(cancellationToken);

        return randomReleaseId is null
            ? null
            : await GetRelease(randomReleaseId.Value, userId, cancellationToken);
    }

    public async Task<IReadOnlyList<RandomReleaseChoiceViewModel>> GetRandomReleaseChoicesForUser(
        string userId,
        int count,
        CancellationToken cancellationToken = default)
    {
        if (count <= 0)
            return [];

        var userReleaseIds = _db.DiscogsReleaseToUsers.AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => x.DiscogsReleaseId)
            .Distinct();

        var releaseCount = await userReleaseIds.CountAsync(cancellationToken);
        if (releaseCount == 0)
            return [];

        var discogsCoverSubfolderName =
            await DiscogsCoverSubfolder.TryGetNameForSignedInUserAsync(_db, userId, cancellationToken);

        var offsets = PickRandomOffsets(releaseCount, Math.Min(count, releaseCount));
        var releaseIds = new List<int>(offsets.Count);
        foreach (var offset in offsets)
        {
            var releaseId = await userReleaseIds
                .OrderBy(x => x)
                .Skip(offset)
                .Select(x => (int?)x)
                .FirstOrDefaultAsync(cancellationToken);

            if (releaseId is not null)
                releaseIds.Add(releaseId.Value);
        }

        var choices = await _db.Releases.AsNoTracking()
            .Where(x => releaseIds.Contains(x.DiscogsReleaseId))
            .Select(x => new
            {
                x.DiscogsReleaseId,
                x.Album,
                x.Images!.LocalThumbnailFilename,
                x.Images!.LocalImageFilename,
                x.Images!.CoverUrl,
            })
            .ToListAsync(cancellationToken);

        return choices
            .OrderBy(x => releaseIds.IndexOf(x.DiscogsReleaseId))
            .Select(x => new RandomReleaseChoiceViewModel
            {
                ReleaseId = x.DiscogsReleaseId,
                Album = x.Album,
                CoverUrl = CoverImageUrlResolver.ResolveReleaseCoverForGrid(
                    discogsCoverSubfolderName,
                    x.LocalThumbnailFilename,
                    x.LocalImageFilename,
                    x.CoverUrl),
            })
            .ToList();
    }

    private static List<int> PickRandomOffsets(int releaseCount, int count)
    {
        var seenOffsets = new HashSet<int>();
        var offsets = new List<int>(count);
        while (offsets.Count < count)
        {
            var offset = Random.Shared.Next(releaseCount);
            if (seenOffsets.Add(offset))
                offsets.Add(offset);
        }

        return offsets;
    }
}
