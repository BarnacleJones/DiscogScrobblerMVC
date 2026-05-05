using DiscogScrobblerMVC.Models;

namespace DiscogScrobblerMVC.Services;

public interface IArtistService
{
    Task<ArtistViewModel?> GetArtist(int id);
}
