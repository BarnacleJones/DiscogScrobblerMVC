using System.Threading.Channels;
using DiscogScrobblerMVC.Services.Background;
using DiscogScrobblerMVC.Services.Interfaces;

namespace DiscogScrobblerMVC.Services.Queues;

/// <summary>
/// Artist/label Discogs profile refresh can take a long time (rate limits, many rows). Settings enqueues;
/// <see cref="DiscogsMetadataRefreshHostedService"/> runs work in the background.
/// </summary>
public sealed class DiscogsMetadataRefreshQueue : IDiscogsMetadataRefreshQueue
{
    private readonly Channel<byte> _channel = Channel.CreateUnbounded<byte>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false
    });

    public bool EnqueueRefreshAllArtistLabelDetails() => _channel.Writer.TryWrite(0);

    public async ValueTask DequeueRefreshAllArtistLabelDetailsAsync(CancellationToken cancellationToken)
    {
        await _channel.Reader.ReadAsync(cancellationToken);
    }
}
