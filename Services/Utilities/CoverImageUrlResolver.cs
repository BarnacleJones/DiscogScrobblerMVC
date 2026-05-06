using DiscogScrobblerMVC.Data.Entities;

namespace DiscogScrobblerMVC.Services;

public static class CoverImageUrlResolver
{
    private const string LocalImagesRequestPath = "/images";

    // Grids/lists should prefer the lightest local variant.
    public static string? ResolveForGrid(DiscogsReleaseImages? images) =>
        ResolveForGrid(images?.LocalThumbnailFilename, images?.LocalImageFilename, images?.CoverUrl);

    public static string? ResolveForGrid(string? localThumbnailFilename, string? localImageFilename, string? fallbackCoverUrl) =>
        ResolveLocalRequestPath(localThumbnailFilename)
        ?? ResolveLocalRequestPath(localImageFilename)
        ?? fallbackCoverUrl;

    // Hero images prioritize full-size local files for better detail.
    public static string? ResolveForHero(DiscogsReleaseImages? images) =>
        ResolveForHero(images?.LocalImageFilename, images?.CoverUrl);

    public static string? ResolveForHero(string? localImageFilename, string? fallbackCoverUrl) =>
        ResolveLocalRequestPath(localImageFilename)
        ?? fallbackCoverUrl;

    private static string? ResolveLocalRequestPath(string? filename)
    {
        if (string.IsNullOrWhiteSpace(filename))
            return null;

        var safeFileName = Path.GetFileName(filename);
        if (string.IsNullOrWhiteSpace(safeFileName))
            return null;

        return $"{LocalImagesRequestPath}/{Uri.EscapeDataString(safeFileName)}";
    }
}
