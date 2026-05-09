using DiscogScrobblerMVC.Services.Interfaces;

namespace DiscogScrobblerMVC.Services.Background;

public class DiscogsBackgroundService : BackgroundService
{
    private readonly ILogger<DiscogsBackgroundService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeSpan _interval = TimeSpan.FromDays(1);

    public DiscogsBackgroundService(
        ILogger<DiscogsBackgroundService> logger,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Discogs background service started.");

        // Delay on startup so the app is fully ready
        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunJobAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error running Discogs sync job.");
            }

            await Task.Delay(_interval, stoppingToken);
        }
    }

    private async Task RunJobAsync(CancellationToken cancellationToken)
    {
        // Use a scope to resolve scoped/transient services
        await using var scope = _scopeFactory.CreateAsyncScope();

        var discogsService = scope.ServiceProvider.GetRequiredService<IDiscogsService>();

        _logger.LogInformation("Starting Discogs sync at {Time}", DateTimeOffset.Now);

        await discogsService.SyncCollectionInBackground(cancellationToken);
        await discogsService.SyncReleaseDetails(cancellationToken);
        await discogsService.DownloadMissingImages(cancellationToken);

        _logger.LogInformation("Discogs sync complete at {Time}", DateTimeOffset.Now);
    }
}