using DiscogsApiClient.Contract;

namespace DiscogScrobblerMVC.Services.Discogs;

internal static class DiscogsApiImages
{
    public static string? PrimaryOrFirstUri(IReadOnlyList<Image>? images) =>
        images?.FirstOrDefault(x => x.Type == ImageType.Primary)?.ImageUri
        ?? images?.FirstOrDefault()?.ImageUri;
}
