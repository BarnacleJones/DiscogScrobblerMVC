using DiscogScrobblerMVC.Data;
using DiscogScrobblerMVC.Models;
using DiscogScrobblerMVC.Services.Interfaces;
using DiscogScrobblerMVC.Services.Utilities;
using Microsoft.EntityFrameworkCore;

namespace DiscogScrobblerMVC.Services;

public class CollectionBrowseService : ICollectionBrowseService
{
    private readonly ApplicationDbContext _db;

    public CollectionBrowseService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<CollectionBrowseViewModel> GetByYearAsync(string userId, int year, CancellationToken cancellationToken = default)
    {
        var discogsCoverSubfolderName = await DiscogsCoverSubfolder.TryGetNameForSignedInUserAsync(
            _db, userId, cancellationToken);

        var releases = await _db.DiscogsReleaseToUsers.AsNoTracking()
            .Where(x => x.UserId == userId && x.Release.Year == year)
            .Select(x => new ReleaseCardQueryResult(
                x.Release.DiscogsReleaseId,
                x.Release.Album,
                x.Release.Year,
                x.Release.Images!.LocalThumbnailFilename,
                x.Release.Images!.LocalImageFilename,
                x.Release.Images!.CoverUrl))
            .ToListAsync(cancellationToken);

        return BuildBrowseViewModel("Year", year.ToString(), discogsCoverSubfolderName, releases);
    }

    public async Task<CollectionBrowseViewModel?> GetByGenreIdAsync(string userId, int genreId, CancellationToken cancellationToken = default)
    {
        var genre = await _db.Genres.AsNoTracking().FirstOrDefaultAsync(g => g.Id == genreId, cancellationToken);
        if (genre is null)
            return null;

        var discogsCoverSubfolderName = await DiscogsCoverSubfolder.TryGetNameForSignedInUserAsync(
            _db, userId, cancellationToken);

        var releases = await _db.DiscogsReleaseToUsers.AsNoTracking()
            .Where(x => x.UserId == userId && x.Release.GenreLinks.Any(y => y.GenreId == genreId))
            .Select(x => new ReleaseCardQueryResult(
                x.Release.DiscogsReleaseId,
                x.Release.Album,
                x.Release.Year,
                x.Release.Images!.LocalThumbnailFilename,
                x.Release.Images!.LocalImageFilename,
                x.Release.Images!.CoverUrl))
            .ToListAsync(cancellationToken);

        return BuildBrowseViewModel("Genre", genre.Name, discogsCoverSubfolderName, releases);
    }

    public async Task<CollectionBrowseViewModel?> GetByStyleIdAsync(string userId, int styleId, CancellationToken cancellationToken = default)
    {
        var style = await _db.Styles.AsNoTracking().FirstOrDefaultAsync(s => s.Id == styleId, cancellationToken);
        if (style is null)
            return null;

        var discogsCoverSubfolderName = await DiscogsCoverSubfolder.TryGetNameForSignedInUserAsync(
            _db, userId, cancellationToken);

        var releases = await _db.DiscogsReleaseToUsers.AsNoTracking()
            .Where(x => x.UserId == userId && x.Release.StyleLinks.Any(y => y.StyleId == styleId))
            .Select(x => new ReleaseCardQueryResult(
                x.Release.DiscogsReleaseId,
                x.Release.Album,
                x.Release.Year,
                x.Release.Images!.LocalThumbnailFilename,
                x.Release.Images!.LocalImageFilename,
                x.Release.Images!.CoverUrl))
            .ToListAsync(cancellationToken);

        return BuildBrowseViewModel("Style", style.Name, discogsCoverSubfolderName, releases);
    }

    public async Task<IReadOnlyList<CollectionBrowseGridRowViewModel>> GetGenreReleaseCountsAsync(string userId,
        CancellationToken cancellationToken = default) =>
        await _db.Genres.AsNoTracking()
            .Where(x => x.ReleaseLinks.Any(y => y.Release.UserAssociations.Any(u => u.UserId == userId)))
            .Select(x => new CollectionBrowseGridRowViewModel
            {
                Id = x.Id,
                Name = x.Name,
                ReleaseCount = x.ReleaseLinks.Count(y => y.Release.UserAssociations.Any(u => u.UserId == userId)),
            })
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<CollectionBrowseGridRowViewModel>> GetStyleReleaseCountsAsync(string userId,
        CancellationToken cancellationToken = default) =>
        await _db.Styles.AsNoTracking()
            .Where(x => x.ReleaseLinks.Any(y => y.Release.UserAssociations.Any(u => u.UserId == userId)))
            .Select(x => new CollectionBrowseGridRowViewModel
            {
                Id = x.Id,
                Name = x.Name,
                ReleaseCount = x.ReleaseLinks.Count(y => y.Release.UserAssociations.Any(u => u.UserId == userId)),
            })
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<CollectionBrowseGridRowViewModel>> GetLabelReleaseCountsAsync(string userId,
        CancellationToken cancellationToken = default) =>
        await _db.Labels.AsNoTracking()
            .Where(x => x.Releases.Any(y => y.UserAssociations.Any(u => u.UserId == userId)))
            .Select(x => new CollectionBrowseGridRowViewModel
            {
                Id = x.Id,
                Name = x.Name,
                ReleaseCount = x.Releases.Count(y => y.UserAssociations.Any(u => u.UserId == userId)),
            })
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

    private static CollectionBrowseViewModel BuildBrowseViewModel(
        string dimensionLabel,
        string valueTitle,
        string? discogsCoverSubfolderName,
        IEnumerable<ReleaseCardQueryResult> releaseRows)
    {
        var releases = releaseRows
            .Select(x => ToCard(discogsCoverSubfolderName, x))
            .OrderBy(r => r.Album, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new CollectionBrowseViewModel
        {
            DimensionLabel = dimensionLabel,
            ValueTitle = valueTitle,
            Releases = releases,
        };
    }

    private static CollectionReleaseCardViewModel ToCard(string? discogsCoverSubfolderName, ReleaseCardQueryResult row) =>
        new(
            row.ReleaseId,
            row.Album,
            row.Year,
            CoverImageUrlResolver.ResolveReleaseCoverForGrid(
                discogsCoverSubfolderName,
                row.LocalThumbnailFilename,
                row.LocalImageFilename,
                row.CoverUrl));

    private readonly record struct ReleaseCardQueryResult(
        int ReleaseId,
        string Album,
        int? Year,
        string? LocalThumbnailFilename,
        string? LocalImageFilename,
        string? CoverUrl);
}
