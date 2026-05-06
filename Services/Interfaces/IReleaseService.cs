using DiscogScrobblerMVC.Models;

namespace DiscogScrobblerMVC.Services;

public interface IReleaseService
{
    Task<ReleaseViewModel?> GetRelease(int discogsReleaseId, CancellationToken cancellationToken = default);
    Task<ReleaseViewModel?> GetRandomReleaseForUser(string userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RandomReleaseChoiceViewModel>> GetRandomReleaseChoicesForUser(
        string userId,
        int count,
        CancellationToken cancellationToken = default);
}
