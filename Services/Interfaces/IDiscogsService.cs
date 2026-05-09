namespace DiscogScrobblerMVC.Services.Interfaces;

public interface IDiscogsService
{
    Task SyncCollection(string discogsUsername, string userId);
    Task SyncCollectionInBackground(CancellationToken ct);
    Task SyncUserInBackground(string applicationUserId, CancellationToken ct);
    Task SyncUserFullInBackground(string applicationUserId, CancellationToken ct);
    Task DownloadMissingImages(CancellationToken ct, string? restrictToApplicationUserId = null);
    Task SyncReleaseDetails(CancellationToken ct, string? restrictToApplicationUserId = null);

    /// <summary>
    /// Re-download Discogs profiles for every artist/label row with IDs. Existing stored image URLs are left unchanged unless empty.
    /// </summary>
    Task RefreshAllArtistLabelDiscogsDetailsAsync(CancellationToken ct);
}