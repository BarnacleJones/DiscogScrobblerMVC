namespace DiscogScrobblerMVC.Services.Interfaces;

public interface IDiscogsSyncQueue
{
    bool EnqueueUserFullSync(string applicationUserId);

    ValueTask<string> DequeueUserFullSyncAsync(CancellationToken cancellationToken);
}

