using DiscogsApiClient;
using DiscogScrobblerMVC.Data;
using DiscogScrobblerMVC.Models;
using Microsoft.EntityFrameworkCore;

namespace DiscogScrobblerMVC.Services;

public class ReleaseService : IReleaseService
{
    private readonly ApplicationDbContext _db;
    private readonly IDiscogsApiClient _discogsApiClient;
    private readonly ILogger<ReleaseService> _logger;

    public ReleaseService(ApplicationDbContext db, IDiscogsApiClient discogsApiClient, ILogger<ReleaseService> logger)
    {
        _db = db;
        _discogsApiClient = discogsApiClient;
        _logger = logger;
    }

    public async Task<ReleaseViewModel?> GetRelease(int discogsReleaseId)
    {
        var release = await _db.Releases
            .Include(r => r.Artists)
            .Include(r => r.Labels)
            .Include(r => r.Tracks)
            .Include(r => r.Images)
            .FirstOrDefaultAsync(r => r.DiscogsReleaseId == discogsReleaseId);

        if (release is null)
            return null;

        var have = 0;
        var want = 0;
        try
        {
            var community = await _discogsApiClient.GetRelease(discogsReleaseId);
            have = community.CommunityStatistics?.UsersOwningReleaseCount ?? 0;
            want = community.CommunityStatistics?.UsersWantingReleaseCount ?? 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch community stats for release {Id}", discogsReleaseId);
        }

        return new ReleaseViewModel
        {
            ReleaseId = release.DiscogsReleaseId,
            Album = release.Album,
            Year = release.Year,
            CoverUrl = release.Images?.CoverUrl,
            Have = have,
            Want = want,
            Artists = release.Artists.Select(a => new ArtistLinkViewModel(a.Id, a.Name)).ToList(),
            Labels = release.Labels.Select(l => new LabelLinkViewModel(l.Id, l.Name)).ToList(),
            Genres = release.Genres?.Split(", ", StringSplitOptions.RemoveEmptyEntries).ToList() ?? [],
            Styles = release.Styles?.Split(", ", StringSplitOptions.RemoveEmptyEntries).ToList() ?? [],
            Tracklist = release.Tracks
                .OrderBy(t => t.Position)
                .Select(t => new TrackViewModel { Position = t.Position, Title = t.Title, Duration = t.Duration })
                .ToList(),
        };
    }
}
