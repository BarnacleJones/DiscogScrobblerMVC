using DiscogScrobblerMVC.Data.Entities;

namespace DiscogScrobblerMVC.Services.Utilities;

public static class CoverImageUrlResolver
{
    private const string LocalImagesRequestPath = "/images";

    public static string? ResolveReleaseCoverForGrid(DiscogsReleaseImages? images, string? discogsCoverSubfolderName) =>
        ResolveReleaseCoverForGrid(
            discogsCoverSubfolderName,
            images?.LocalThumbnailFilename,
            images?.LocalImageFilename,
            images?.CoverUrl);

    public static string? ResolveReleaseCoverForGrid(
        string? discogsCoverSubfolderName,
        string? localThumbnailFilename,
        string? localImageFilename,
        string? fallbackCoverUrl) =>
        ResolveUrlForFileUnderDiscogsUserFolder(discogsCoverSubfolderName, localThumbnailFilename)
        ?? ResolveUrlForFileUnderDiscogsUserFolder(discogsCoverSubfolderName, localImageFilename)
        ?? fallbackCoverUrl;

    public static string? ResolveReleaseCoverForHero(DiscogsReleaseImages? images, string? discogsCoverSubfolderName) =>
        ResolveReleaseCoverForHero(
            discogsCoverSubfolderName,
            images?.LocalImageFilename,
            images?.CoverUrl);

    public static string? ResolveReleaseCoverForHero(
        string? discogsCoverSubfolderName,
        string? localImageFilename,
        string? fallbackCoverUrl) =>
        ResolveUrlForFileUnderDiscogsUserFolder(discogsCoverSubfolderName, localImageFilename)
        ?? fallbackCoverUrl;

    public static string? ResolveArtistProfileImageForGrid(
        string? localThumbnailFilename,
        string? localImageFilename,
        string? fallbackCoverUrl) =>
        ResolveSharedCatalogFileUrl(CoverStoragePathResolver.SharedArtistProfileSubfolder, localThumbnailFilename)
        ?? ResolveSharedCatalogFileUrl(CoverStoragePathResolver.SharedArtistProfileSubfolder, localImageFilename)
        ?? fallbackCoverUrl;

    public static string? ResolveLabelProfileImageForGrid(
        string? localThumbnailFilename,
        string? localImageFilename,
        string? fallbackCoverUrl) =>
        ResolveSharedCatalogFileUrl(CoverStoragePathResolver.SharedLabelProfileSubfolder, localThumbnailFilename)
        ?? ResolveSharedCatalogFileUrl(CoverStoragePathResolver.SharedLabelProfileSubfolder, localImageFilename)
        ?? fallbackCoverUrl;

    private static string? ResolveSharedCatalogFileUrl(string catalogSubfolder, string? filename)
    {
        if (string.IsNullOrWhiteSpace(filename))
            return null;

        var safeFileName = Path.GetFileName(filename);
        if (string.IsNullOrWhiteSpace(safeFileName))
            return null;

        return $"{LocalImagesRequestPath}/{Uri.EscapeDataString(catalogSubfolder)}/{Uri.EscapeDataString(safeFileName)}";
    }

    private static string? ResolveUrlForFileUnderDiscogsUserFolder(string? discogsCoverSubfolderName, string? filename)
    {
        if (string.IsNullOrWhiteSpace(discogsCoverSubfolderName) || string.IsNullOrWhiteSpace(filename))
            return null;

        var safeFileName = Path.GetFileName(filename);
        if (string.IsNullOrWhiteSpace(safeFileName))
            return null;

        return
            $"{LocalImagesRequestPath}/{Uri.EscapeDataString(discogsCoverSubfolderName)}/{Uri.EscapeDataString(safeFileName)}";
    }
}
