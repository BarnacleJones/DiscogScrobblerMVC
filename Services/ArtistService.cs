using DiscogsApiClient;
using DiscogScrobblerMVC.Data;
using DiscogScrobblerMVC.Models;
using Microsoft.EntityFrameworkCore;

namespace DiscogScrobblerMVC.Services;

public class ArtistService : IArtistService
{
    private readonly ApplicationDbContext _db;
    private readonly IDiscogsApiClient _discogsApiClient;
    private readonly ILogger<ArtistService> _logger;

    public ArtistService(ApplicationDbContext db, IDiscogsApiClient discogsApiClient, ILogger<ArtistService> logger)
    {
        _db = db;
        _discogsApiClient = discogsApiClient;
        _logger = logger;
    }

    public async Task<ArtistViewModel?> GetArtist(int id)
    {
        var artist = await _db.Artists
            .Include(a => a.Releases)
            .ThenInclude(r => r.Images)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (artist is null)
            return null;

        string? profile = null;
        string? imageUrl = null;

        if (artist.DiscogsArtistId.HasValue)
        {
            try
            {
                var details = await _discogsApiClient.GetArtist(artist.DiscogsArtistId.Value);
                profile = details.Profile;
                imageUrl = details.Images?.FirstOrDefault(i => i.Type == DiscogsApiClient.Contract.ImageType.Primary)?.ImageUri
                    ?? details.Images?.FirstOrDefault()?.ImageUri;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch Discogs artist details for {Name} ({DiscogsArtistId})", artist.Name, artist.DiscogsArtistId);
            }
        }

        return new ArtistViewModel
        {
            Id = artist.Id,
            Name = artist.Name,
            Profile = profile,
            ImageUrl = imageUrl,
            CollectionReleases = artist.Releases
                .Select(r => new CollectionReleaseCardViewModel(r.DiscogsReleaseId, r.Album, r.Year, r.Images?.CoverUrl))
                .OrderByDescending(r => r.Year)
                .ToList(),
        };
    }
}
