// Services/LastFmService.cs
using Hqub.Lastfm;
using Hqub.Lastfm.Entities;

public interface ILastFmService
{
    Task<bool> AuthenticateAsync(string username, string password);
    Task<bool> ScrobbleAlbumAsync(string artist, string album, IList<string> trackNames);
}

public class LastFmService : ILastFmService
{
    private readonly LastfmClient _client;
    private readonly ILogger<LastFmService> _logger;

    public LastFmService(LastfmClient client, ILogger<LastFmService> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<bool> AuthenticateAsync(string username, string password)
    {
        try
        {
            await _client.AuthenticateAsync(username, password);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Last.fm auth failed for {Username}", username);
            return false;
        }
    }

    public async Task<bool> ScrobbleAlbumAsync(
        string artist, string album, IList<string> trackNames)
    {
        if (!_client.Session.Authenticated)
            throw new InvalidOperationException("Not authenticated with Last.fm");

        // Build scrobbles — timestamps work backwards from now,
        // each track assumed ~3 min apart (real duration would be better)
        var now = DateTimeOffset.UtcNow;
        var scrobbles = trackNames
            .Select((title, i) => new Scrobble()
            {
                Artist = artist,
                Album = album,
                Track = title,
                Date = DateTime.Now.AddMinutes(-(trackNames.Count - i) * 3)
            })
            .ToList();

        try
        {
            // Hqub supports batch scrobbling (up to 50 at a time per Last.fm API)
            var response = await _client.Track.ScrobbleAsync(scrobbles);
            _logger.LogInformation(
                "Scrobbled {Count} tracks for {Artist} - {Album}",
                scrobbles.Count, artist, album);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Scrobble failed for {Artist} - {Album}", artist, album);
            return false;
        }
    }
}