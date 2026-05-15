namespace DiscogScrobblerMVC.Services.Interfaces;

public interface IDiscogsService
{
    Task SyncCollection(string discogsUsername, string applicationUserId);
    Task SyncCollectionInBackground(CancellationToken cancellationToken);
    Task SyncUserInBackground(string applicationUserId, CancellationToken cancellationToken);
    Task SyncUserFullInBackground(string applicationUserId, CancellationToken cancellationToken);
    Task DownloadMissingImages(CancellationToken cancellationToken, string? restrictToApplicationUserId = null);
    Task SyncReleaseDetails(CancellationToken cancellationToken, string? restrictToApplicationUserId = null);

    /// <summary>
    /// Re-download Discogs profiles for artist/label rows behind schema version (GET artist/label).
    /// When <paramref name="restrictToApplicationUserId"/> is set, only entities linked to that user's collection releases are refreshed.
    /// Existing stored image URLs are left unchanged unless empty.
    /// </summary>
    Task RefreshAllArtistLabelDiscogsDetailsAsync(CancellationToken cancellationToken, string? restrictToApplicationUserId = null);

    /// <summary>
    /// Resets Discogs-detail schema versions for the user's collection releases and linked artists/labels, then re-syncs release details, artist/label profiles, and missing images (scoped to that user).
    /// </summary>
    Task ForceRefreshUserDiscogsCachedEntitiesAsync(string applicationUserId, CancellationToken cancellationToken);
}
