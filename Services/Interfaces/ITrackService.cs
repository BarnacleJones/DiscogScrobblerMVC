using DiscogScrobblerMVC.Models;

namespace DiscogScrobblerMVC.Services.Interfaces;

public interface ITrackService
{
    Task<IReadOnlyList<TrackItemViewModel>> GetTracksAsync(string userId, CancellationToken cancellationToken = default);
}

