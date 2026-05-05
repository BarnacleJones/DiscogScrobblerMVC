using DiscogsApiClient;
using DiscogScrobblerMVC.Data;
using DiscogScrobblerMVC.Models;
using Microsoft.EntityFrameworkCore;

namespace DiscogScrobblerMVC.Services;

public class LabelService : ILabelService
{
    private readonly ApplicationDbContext _db;
    private readonly IDiscogsApiClient _discogsApiClient;
    private readonly ILogger<LabelService> _logger;

    public LabelService(ApplicationDbContext db, IDiscogsApiClient discogsApiClient, ILogger<LabelService> logger)
    {
        _db = db;
        _discogsApiClient = discogsApiClient;
        _logger = logger;
    }

    public async Task<LabelViewModel?> GetLabel(int id)
    {
        var label = await _db.Labels
            .Include(l => l.Releases)
            .ThenInclude(r => r.Images)
            .FirstOrDefaultAsync(l => l.Id == id);

        if (label is null)
            return null;

        string? profile = null;
        string? imageUrl = null;

        if (label.DiscogsLabelId.HasValue)
        {
            try
            {
                var details = await _discogsApiClient.GetLabel(label.DiscogsLabelId.Value);
                profile = details.Profile;
                imageUrl = details.Images?.FirstOrDefault(i => i.Type == DiscogsApiClient.Contract.ImageType.Primary)?.ImageUri
                    ?? details.Images?.FirstOrDefault()?.ImageUri;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch Discogs label details for {Name} ({DiscogsLabelId})", label.Name, label.DiscogsLabelId);
            }
        }

        return new LabelViewModel
        {
            Id = label.Id,
            Name = label.Name,
            Profile = profile,
            ImageUrl = imageUrl,
            CollectionReleases = label.Releases
                .Select(r => new CollectionReleaseCardViewModel(r.DiscogsReleaseId, r.Album, r.Year, r.Images?.CoverUrl))
                .OrderByDescending(r => r.Year)
                .ToList(),
        };
    }
}
