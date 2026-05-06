using DiscogScrobblerMVC.Models;

namespace DiscogScrobblerMVC.Services;

public interface IStatsService
{
    Task<StatsViewModel> GetStatsAsync(string userId, CancellationToken cancellationToken = default);
}
