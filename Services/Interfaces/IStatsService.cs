using DiscogScrobblerMVC.Models;

namespace DiscogScrobblerMVC.Services.Interfaces;

public interface IStatsService
{
    Task<StatsViewModel> GetStatsAsync(string userId, CancellationToken cancellationToken = default);
}
