using System.Threading.Channels;
using DiscogScrobblerMVC.Services.Interfaces;

namespace DiscogScrobblerMVC.Services.Queues;

// Discogs sync jobs can take a long time (API rate limits, many releases). The Settings page only
// needs to enqueue work; DiscogsOnDemandSyncService runs it in the background so the HTTP response
// returns immediately and sync logic stays scoped to a BackgroundService scope like the daily job.
public class DiscogsSyncQueue : IDiscogsSyncQueue
{
    private readonly Channel<DiscogsQueuedWork> _channel = Channel.CreateUnbounded<DiscogsQueuedWork>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false
    });

    public bool EnqueueUserFullSync(string applicationUserId) =>
        _channel.Writer.TryWrite(new DiscogsQueuedWork(DiscogsQueuedWorkKind.FullSync, applicationUserId));

    public bool EnqueueForceRefreshUserDiscogsCache(string applicationUserId) =>
        _channel.Writer.TryWrite(new DiscogsQueuedWork(DiscogsQueuedWorkKind.ForceRefreshUserCollection, applicationUserId));

    public ValueTask<DiscogsQueuedWork> DequeueAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAsync(cancellationToken);
}
