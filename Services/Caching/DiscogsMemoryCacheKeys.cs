namespace DiscogScrobblerMVC.Services;

internal static class DiscogsMemoryCacheKeys
{
    public static string ArtistDetails(int discogsArtistId) => $"discogs:artist-details:{discogsArtistId}";

    public static string LabelDetails(int discogsLabelId) => $"discogs:label-details:{discogsLabelId}";
}
