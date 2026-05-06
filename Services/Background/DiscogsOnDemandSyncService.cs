namespace DiscogScrobblerMVC.Services;

public class DiscogsOnDemandSyncService : BackgroundService
{
    private readonly ILogger<DiscogsOnDemandSyncService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IDiscogsSyncQueue _queue;

    public DiscogsOnDemandSyncService(
        ILogger<DiscogsOnDemandSyncService> logger,
        IServiceScopeFactory scopeFactory,
        IDiscogsSyncQueue queue)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _queue = queue;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Discogs on-demand sync service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var userId = await _queue.DequeueUserFullSyncAsync(stoppingToken);

                await using var scope = _scopeFactory.CreateAsyncScope();
                var discogsService = scope.ServiceProvider.GetRequiredService<IDiscogsService>();

                _logger.LogInformation("Starting on-demand Discogs sync for user {UserId}", userId);
                await discogsService.SyncUserFullInBackground(userId, stoppingToken);
                _logger.LogInformation("On-demand Discogs sync complete for user {UserId}", userId);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // graceful shutdown
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error running on-demand Discogs sync job.");
            }
        }
    }
}

