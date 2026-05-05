namespace DiscogScrobblerMVC.Services;

public interface ILastFmService
{
    Task<bool> AuthenticateAsync(string username, string password);
    Task<bool> ScrobbleAlbumAsync(string artist, string album, IList<string> trackNames);
}
