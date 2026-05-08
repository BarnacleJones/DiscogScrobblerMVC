using DiscogScrobblerMVC.Services.Interfaces;

namespace DiscogScrobblerMVC.Services.Background;

public sealed class DiscogsMetadataRefreshHostedService : BackgroundService
{
    private readonly ILogger<DiscogsMetadataRefreshHostedService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IDiscogsMetadataRefreshQueue _queue;

    public DiscogsMetadataRefreshHostedService(
        ILogger<DiscogsMetadataRefreshHostedService> logger,
        IServiceScopeFactory scopeFactory,
        IDiscogsMetadataRefreshQueue queue)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _queue = queue;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Discogs artist/label metadata refresh queue started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _queue.DequeueRefreshAllArtistLabelDetailsAsync(stoppingToken);

                await using var scope = _scopeFactory.CreateAsyncScope();
                var discogsService = scope.ServiceProvider.GetRequiredService<IDiscogsService>();

                _logger.LogInformation("Starting on-demand Discogs artist/label metadata refresh.");
                await discogsService.RefreshAllArtistLabelDiscogsDetailsAsync(stoppingToken);
                await discogsService.DownloadMissingImages(stoppingToken);
                _logger.LogInformation("On-demand Discogs artist/label metadata refresh complete.");
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // graceful shutdown
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error running Discogs artist/label metadata refresh job.");
            }
        }
    }
}
