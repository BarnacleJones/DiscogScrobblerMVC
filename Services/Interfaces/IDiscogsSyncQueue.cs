namespace DiscogScrobblerMVC.Services.Interfaces;

public interface IDiscogsSyncQueue
{
    bool EnqueueUserFullSync(string applicationUserId);

    bool EnqueueForceRefreshUserDiscogsCache(string applicationUserId);

    ValueTask<DiscogsQueuedWork> DequeueAsync(CancellationToken cancellationToken);
}
