using DiscogScrobblerMVC.Data;
using DiscogScrobblerMVC.Models;
using DiscogScrobblerMVC.Services.Interfaces;
using DiscogScrobblerMVC.Services.Utilities;
using Microsoft.EntityFrameworkCore;

namespace DiscogScrobblerMVC.Services;

public class CollectionService : ICollectionService
{
    private const int SearchArtistReleaseLimit = 50;

    private readonly ApplicationDbContext _db;

    public CollectionService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<CollectionItemViewModel>> GetCollectionItemsAsync(string userId, CancellationToken cancellationToken = default)
    {
        var discogsCoverSubfolderName = await DiscogsCoverSubfolder.TryGetNameForSignedInUserAsync(
            _db, userId, cancellationToken);

        var collectionItems = await _db.DiscogsReleaseToUsers.AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.DateAdded)
            .Select(x => new
            {
                ReleaseId = x.Release.DiscogsReleaseId,
                x.Release.DiscogsMasterId,
                x.Release.Album,
                x.Release.Year,
                x.DateAdded,
                x.Release.Images!.LocalThumbnailFilename,
                x.Release.Images!.LocalImageFilename,
                x.Release.Images!.CoverUrl,
                Artists = x.Release.Artists
                    .OrderBy(y => y.Name)
                    .Select(y => new ArtistLinkViewModel(y.Id, y.Name))
                    .ToList(),
            })
            .ToListAsync(cancellationToken);

        return collectionItems
            .Select(x => new CollectionItemViewModel
            {
                ReleaseId = x.ReleaseId,
                DiscogsMasterId = x.DiscogsMasterId,
                ArtistDisplay = FormatArtistDisplay(x.Artists),
                Artists = x.Artists,
                Album = x.Album,
                Year = x.Year,
                DateAdded = x.DateAdded,
                CoverUrl = CoverImageUrlResolver.ResolveReleaseCoverForGrid(
                    discogsCoverSubfolderName,
                    x.LocalThumbnailFilename,
                    x.LocalImageFilename,
                    x.CoverUrl),
            })
            .ToList();
    }

    public async Task<IReadOnlyList<HomeRecentReleaseViewModel>> GetRecentCollectionReleasesAsync(
        string userId, int take, CancellationToken cancellationToken = default)
    {
        var discogsCoverSubfolderName = await DiscogsCoverSubfolder.TryGetNameForSignedInUserAsync(
            _db, userId, cancellationToken);

        var recentReleases = await _db.DiscogsReleaseToUsers.AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.DateAdded)
            .Take(take)
            .Select(x => new
            {
                ReleaseId = x.Release.DiscogsReleaseId,
                x.Release.Album,
                x.Release.Images!.LocalThumbnailFilename,
                x.Release.Images!.LocalImageFilename,
                x.Release.Images!.CoverUrl,
                Artists = x.Release.Artists
                    .OrderBy(y => y.Name)
                    .Select(y => new ArtistLinkViewModel(y.Id, y.Name))
                    .ToList(),
            })
            .ToListAsync(cancellationToken);

        return recentReleases
            .Select(x => new HomeRecentReleaseViewModel
            {
                ReleaseId = x.ReleaseId,
                Album = x.Album,
                ArtistDisplay = FormatArtistDisplay(x.Artists),
                Artists = x.Artists,
                CoverUrl = CoverImageUrlResolver.ResolveReleaseCoverForGrid(
                    discogsCoverSubfolderName,
                    x.LocalThumbnailFilename,
                    x.LocalImageFilename,
                    x.CoverUrl),
            })
            .ToList();
    }

    public async Task<CollectionSearchViewModel> SearchCollectionAsync(
        string userId, string? query, CancellationToken cancellationToken = default)
    {
        var trimmedSearchQuery = query?.Trim() ?? "";
        var viewModel = new CollectionSearchViewModel { Query = trimmedSearchQuery };
        if (string.IsNullOrEmpty(trimmedSearchQuery))
            return viewModel;

        var discogsCoverSubfolderName = await DiscogsCoverSubfolder.TryGetNameForSignedInUserAsync(
            _db, userId, cancellationToken);

        var searchPattern = $"%{EscapeLikePattern(trimmedSearchQuery)}%";

        var artists = await _db.Artists.AsNoTracking()
            .Where(x => EF.Functions.Like(x.Name, searchPattern, "\\"))
            .Where(x => x.Releases.Any(y => y.UserAssociations.Any(z => z.UserId == userId)))
            .OrderBy(x => x.Name)
            .Take(SearchArtistReleaseLimit)
            .Select(x => new CollectionSearchArtistResult { Id = x.Id, Name = x.Name })
            .ToListAsync(cancellationToken);

        var releases = await _db.DiscogsReleaseToUsers.AsNoTracking()
            .Where(x => x.UserId == userId)
            .Where(x =>
                EF.Functions.Like(x.Release.Album, searchPattern, "\\") ||
                x.Release.Artists.Any(y => EF.Functions.Like(y.Name, searchPattern, "\\")))
            .GroupBy(x => new
            {
                x.Release.DiscogsReleaseId,
                x.Release.Album,
                x.Release.Year,
                x.Release.Images!.LocalThumbnailFilename,
                x.Release.Images!.LocalImageFilename,
                x.Release.Images!.CoverUrl,
            })
            .OrderBy(x => x.Key.Album)
            .Take(SearchArtistReleaseLimit)
            .Select(g => new
            {
                ReleaseId = g.Key.DiscogsReleaseId,
                g.Key.Album,
                g.Key.Year,
                g.Key.LocalThumbnailFilename,
                g.Key.LocalImageFilename,
                g.Key.CoverUrl,
                Artists = _db.Releases
                    .Where(x => x.DiscogsReleaseId == g.Key.DiscogsReleaseId)
                    .SelectMany(x => x.Artists)
                    .OrderBy(x => x.Name)
                    .Select(x => x.Name)
                    .ToList(),
            })
            .ToListAsync(cancellationToken);

        viewModel.Artists = artists;
        viewModel.Releases = releases
            .Select(x => new CollectionSearchReleaseResult
            {
                ReleaseId = x.ReleaseId,
                Album = x.Album,
                Year = x.Year,
                ArtistDisplay = FormatArtistDisplay(x.Artists),
                CoverUrl = CoverImageUrlResolver.ResolveReleaseCoverForGrid(
                    discogsCoverSubfolderName,
                    x.LocalThumbnailFilename,
                    x.LocalImageFilename,
                    x.CoverUrl),
            })
            .ToList();

        return viewModel;
    }

    private static string FormatArtistDisplay(IReadOnlyCollection<ArtistLinkViewModel> artists) =>
        artists.Count > 0
            ? string.Join(", ", artists.Select(x => x.Name))
            : "—";

    private static string FormatArtistDisplay(IReadOnlyCollection<string> artists) =>
        artists.Count > 0
            ? string.Join(", ", artists)
            : "—";

    private static string EscapeLikePattern(string value) =>
        value
            .Replace("\\", "\\\\")
            .Replace("%", "\\%")
            .Replace("_", "\\_");
}
