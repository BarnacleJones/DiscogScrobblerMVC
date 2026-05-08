using DiscogScrobblerMVC.Models;

namespace DiscogScrobblerMVC.Services.Interfaces;

public interface IArtistService
{
    Task<ArtistViewModel?> GetArtist(int id, string viewerApplicationUserId, CancellationToken cancellationToken = default);
}
