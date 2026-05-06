using DiscogScrobblerMVC.Data;
using DiscogScrobblerMVC.Models;
using Microsoft.EntityFrameworkCore;

namespace DiscogScrobblerMVC.Services;

public class TrackService : ITrackService
{
    private readonly ApplicationDbContext _db;

    public TrackService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<TrackItemViewModel>> GetTracksAsync(string userId, CancellationToken cancellationToken = default)
    {
        // Tracks are owned implicitly via the user owning the Release in DiscogsReleaseToUsers.
        var tracks = await _db.Tracks.AsNoTracking()
            .Where(x => x.Release.UserAssociations.Any(x => x.UserId == userId))
            .OrderBy(x => x.Title)
            .ThenBy(x => x.Position)
            .Select(x => new
            {
                ReleaseId = x.Release.DiscogsReleaseId,
                Album = x.Release.Album,
                Year = x.Release.Year,
                Artists = x.Release.Artists.Select(x => x.Name).ToList(),
                x.Position,
                x.Title,
                x.Duration,
            })
            .ToListAsync(cancellationToken);

        return tracks.Select(x =>
        {
            var artistDisplay = x.Artists.Count > 0
                ? string.Join(", ", x.Artists.OrderBy(x => x))
                : "—";

            return new TrackItemViewModel
            {
                ReleaseId = x.ReleaseId,
                ArtistDisplay = artistDisplay,
                Album = x.Album,
                Year = x.Year,
                Position = x.Position,
                Title = x.Title,
                Duration = x.Duration,
            };
        }).ToList();
    }
}
