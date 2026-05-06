using DiscogScrobblerMVC.Data;
using DiscogScrobblerMVC.Models;
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

        return BuildBrowseViewModel("Year", year.ToString(), releases);
    }

    public async Task<CollectionBrowseViewModel?> GetByGenreIdAsync(string userId, int genreId, CancellationToken cancellationToken = default)
    {
        var genre = await _db.Genres.AsNoTracking().FirstOrDefaultAsync(g => g.Id == genreId, cancellationToken);
        if (genre is null)
            return null;

        var releases = await _db.DiscogsReleaseToUsers.AsNoTracking()
            .Where(x => x.UserId == userId && x.Release.GenreLinks.Any(x => x.GenreId == genreId))
            .Select(x => new ReleaseCardQueryResult(
                x.Release.DiscogsReleaseId,
                x.Release.Album,
                x.Release.Year,
                x.Release.Images!.LocalThumbnailFilename,
                x.Release.Images!.LocalImageFilename,
                x.Release.Images!.CoverUrl))
            .ToListAsync(cancellationToken);

        return BuildBrowseViewModel("Genre", genre.Name, releases);
    }

    public async Task<CollectionBrowseViewModel?> GetByStyleIdAsync(string userId, int styleId, CancellationToken cancellationToken = default)
    {
        var style = await _db.Styles.AsNoTracking().FirstOrDefaultAsync(s => s.Id == styleId, cancellationToken);
        if (style is null)
            return null;

        var releases = await _db.DiscogsReleaseToUsers.AsNoTracking()
            .Where(x => x.UserId == userId && x.Release.StyleLinks.Any(x => x.StyleId == styleId))
            .Select(x => new ReleaseCardQueryResult(
                x.Release.DiscogsReleaseId,
                x.Release.Album,
                x.Release.Year,
                x.Release.Images!.LocalThumbnailFilename,
                x.Release.Images!.LocalImageFilename,
                x.Release.Images!.CoverUrl))
            .ToListAsync(cancellationToken);

        return BuildBrowseViewModel("Style", style.Name, releases);
    }

    private static CollectionBrowseViewModel BuildBrowseViewModel(
        string dimensionLabel,
        string valueTitle,
        IEnumerable<ReleaseCardQueryResult> releaseRows)
    {
        var releases = releaseRows
            .Select(ToCard)
            .OrderBy(r => r.Album, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new CollectionBrowseViewModel
        {
            DimensionLabel = dimensionLabel,
            ValueTitle = valueTitle,
            Releases = releases,
        };
    }

    private static CollectionReleaseCardViewModel ToCard(ReleaseCardQueryResult row) =>
        new(
            row.ReleaseId,
            row.Album,
            row.Year,
            CoverImageUrlResolver.ResolveForGrid(row.LocalThumbnailFilename, row.LocalImageFilename, row.CoverUrl));

    private readonly record struct ReleaseCardQueryResult(
        int ReleaseId,
        string Album,
        int? Year,
        string? LocalThumbnailFilename,
        string? LocalImageFilename,
        string? CoverUrl);
}
