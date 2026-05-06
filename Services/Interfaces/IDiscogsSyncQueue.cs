namespace DiscogScrobblerMVC.Services;

public interface IDiscogsSyncQueue
{
    bool EnqueueUserFullSync(string applicationUserId);

    ValueTask<string> DequeueUserFullSyncAsync(CancellationToken cancellationToken);
}

