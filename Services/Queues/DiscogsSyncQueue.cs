using System.Threading.Channels;

namespace DiscogScrobblerMVC.Services;

// Full Discogs sync can take a long time (API rate limits, many releases). The Settings page only
// needs to enqueue work; DiscogsOnDemandSyncService runs it in the background so the HTTP response
// returns immediately and sync logic stays scoped to a BackgroundService scope like the daily job.
public class DiscogsSyncQueue : IDiscogsSyncQueue
{
    private readonly Channel<string> _channel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
    {
        // One consumer avoids overlapping full syncs competing for Discogs/API and DB locks.
        SingleReader = true,
        SingleWriter = false
    });

    // Returns false if the app is shutting down and the channel writer is closed.
    public bool EnqueueUserFullSync(string applicationUserId)
    {
        return _channel.Writer.TryWrite(applicationUserId);
    }

    public ValueTask<string> DequeueUserFullSyncAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAsync(cancellationToken);
}

