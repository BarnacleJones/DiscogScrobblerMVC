using DiscogScrobblerMVC.Services.Interfaces;

namespace DiscogScrobblerMVC.Services.Background;

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
                var work = await _queue.DequeueAsync(stoppingToken);

                await using var scope = _scopeFactory.CreateAsyncScope();
                var discogsService = scope.ServiceProvider.GetRequiredService<IDiscogsService>();

                switch (work.Kind)
                {
                    case DiscogsQueuedWorkKind.FullSync:
                        _logger.LogInformation("Starting on-demand Discogs full sync for user {UserId}", work.ApplicationUserId);
                        await discogsService.SyncUserFullInBackground(work.ApplicationUserId, stoppingToken);
                        _logger.LogInformation("On-demand Discogs full sync complete for user {UserId}", work.ApplicationUserId);
                        break;
                    case DiscogsQueuedWorkKind.ForceRefreshUserCollection:
                        _logger.LogInformation(
                            "Starting on-demand Discogs force refresh for user {UserId}",
                            work.ApplicationUserId);
                        await discogsService.ForceRefreshUserDiscogsCachedEntitiesAsync(work.ApplicationUserId, stoppingToken);
                        _logger.LogInformation(
                            "On-demand Discogs force refresh complete for user {UserId}",
                            work.ApplicationUserId);
                        break;
                    default:
                        _logger.LogWarning("Unknown Discogs queue kind {Kind} for user {UserId}", work.Kind, work.ApplicationUserId);
                        break;
                }
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
