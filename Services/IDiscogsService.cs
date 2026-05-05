namespace DiscogScrobblerMVC.Services;

public interface IDiscogsService
{
    Task SyncCollection(string discogsUsername, string userId);
    Task SyncCollectionInBackground(CancellationToken ct);
    Task DownloadMissingImages(CancellationToken ct);
}