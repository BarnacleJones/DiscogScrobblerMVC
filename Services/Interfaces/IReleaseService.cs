using DiscogScrobblerMVC.Models;

namespace DiscogScrobblerMVC.Services.Interfaces;

public interface IReleaseService
{
    Task<ReleaseViewModel?> GetRelease(int discogsReleaseId, string viewerApplicationUserId, CancellationToken cancellationToken = default);
    Task<ReleaseViewModel?> GetRandomReleaseForUser(string userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RandomReleaseChoiceViewModel>> GetRandomReleaseChoicesForUser(
        string userId,
        int count,
        CancellationToken cancellationToken = default);
}
