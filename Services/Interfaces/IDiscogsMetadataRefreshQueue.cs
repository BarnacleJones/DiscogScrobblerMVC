namespace DiscogScrobblerMVC.Services.Interfaces;

public interface IDiscogsMetadataRefreshQueue
{
    /// <returns><c>false</c> if the app is shutting down and the channel writer is closed.</returns>
    bool EnqueueRefreshAllArtistLabelDetails();

    ValueTask DequeueRefreshAllArtistLabelDetailsAsync(CancellationToken cancellationToken);
}
