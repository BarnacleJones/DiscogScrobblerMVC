using DiscogScrobblerMVC.Models;

namespace DiscogScrobblerMVC.Services;

public interface ITrackService
{
    Task<IReadOnlyList<TrackItemViewModel>> GetTracksAsync(string userId, CancellationToken cancellationToken = default);
}

