using DiscogScrobblerMVC.Models;

namespace DiscogScrobblerMVC.Services;

public interface IReleaseService
{
    Task<ReleaseViewModel?> GetRelease(int discogsReleaseId);
}
