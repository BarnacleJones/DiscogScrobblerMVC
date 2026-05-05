using DiscogsApiClient;
using DiscogsApiClient.Authentication;
using DiscogsApiClient.Contract.User.Collection;
using DiscogScrobblerMVC.Data;
using DiscogScrobblerMVC.Data.Entities;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore;

namespace DiscogScrobblerMVC.Services;

public class DiscogsService : IDiscogsService
{
    private IDiscogsApiClient _discogsApiClient;
    private IDiscogsAuthenticationService _discogsAuthenticationService;
    private readonly HttpClient _http;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<DiscogsService> _logger;

    public DiscogsService(HttpClient http, IDiscogsApiClient discogsApiClient, IDiscogsAuthenticationService discogsAuthenticationService, ApplicationDbContext db, ILogger<DiscogsService> logger)
    {
        _http = http;
        _discogsApiClient = discogsApiClient;
        _discogsAuthenticationService = discogsAuthenticationService;
        _db = db;
        _logger = logger;
    }


    public void Authenticate(string token)
    {
        _discogsAuthenticationService.AuthenticateWithPersonalAccessToken(token);
    }

    public async Task SyncCollection(string discogsUsername, string userId)
    {
        var page = 1;
        const int perPage = 100;
        var hasMore = true;

        var artistCache = await _db.Artists.ToDictionaryAsync(a => a.Name);
        var labelCache  = await _db.Labels.ToDictionaryAsync(l => l.Name);
        var seenIds     = new HashSet<int>();

        while (hasMore)
        {
            var (pagination, releases) = await _discogsApiClient.GetCollectionFolderReleases(userId, 0);

            foreach (var item in releases)
            {
                await UpsertCollectionItem(userId, item, seenIds, artistCache, labelCache);
            }

            await _db.SaveChangesAsync();

            var pages = pagination.TotalPages;
            hasMore = page < pages;
            page++;
        }

        _logger.LogInformation("Discogs sync complete for {Username}", discogsUsername);
    }

    private async Task UpsertCollectionItem(
        string userId,
        CollectionFolderRelease item,
        HashSet<int> seenIds,
        Dictionary<string, Artist> artistCache,
        Dictionary<string, Label> labelCache)
    {
        // Duplicate DiscogsReleaseId within a batch (e.g. same release in two folders) — skip.
        if (!seenIds.Add(item.Id))
            return;

        var existingRelease = await _db.Releases
            .Include(r => r.Images)
            .Include(r => r.Artists)
            .Include(r => r.Labels)
            .FirstOrDefaultAsync(r => r.DiscogsReleaseId == item.Id);

        var artists = ResolveArtists(item, artistCache);
        var labels  = ResolveLabels(item, labelCache);

        if (existingRelease is null)
        {
            var release = ConstructNewReleaseEntity(item);
            foreach (var a in artists) release.Artists.Add(a);
            foreach (var l in labels)  release.Labels.Add(l);

            _db.Releases.Add(release);
            _db.DiscogsReleaseImages.Add(new DiscogsReleaseImages
            {
                DiscogsReleaseId = item.Id,
                CoverUrl = item.Release.CoverImageUrl,
            });
            _db.DiscogsReleaseToUsers.Add(new DiscogsReleaseToUser
            {
                DiscogsReleaseId = item.Id,
                UserId = userId,
                DateAdded = item.AddedAt,
            });
        }
        else
        {
            // Update cover URL in case it changed (Discogs CDN URLs rotate)
            if (existingRelease.Images is not null)
                existingRelease.Images.CoverUrl = item.Release.CoverImageUrl;

            var existingUserLink = await _db.DiscogsReleaseToUsers
                .FirstOrDefaultAsync(r => r.DiscogsReleaseId == item.Id && r.UserId == userId);

            if (existingUserLink is null)
            {
                _db.DiscogsReleaseToUsers.Add(new DiscogsReleaseToUser
                {
                    DiscogsReleaseId = item.Id,
                    UserId = userId,
                    DateAdded = item.AddedAt,
                });
            }
        }
    }

    private List<Artist> ResolveArtists(CollectionFolderRelease item, Dictionary<string, Artist> cache)
    {
        var result = new List<Artist>();
        foreach (var apiArtist in item.Release.Artists)
        {
            var name = apiArtist.Name?.Trim();
            if (string.IsNullOrEmpty(name)) continue;
            if (!cache.TryGetValue(name, out var artist))
            {
                artist = new Artist { Name = name };
                cache[name] = artist;
                _db.Artists.Add(artist);
            }
            result.Add(artist);
        }
        return result;
    }

    private List<Label> ResolveLabels(CollectionFolderRelease item, Dictionary<string, Label> cache)
    {
        var result = new List<Label>();
        foreach (var apiLabel in item.Release.Labels)
        {
            var name = apiLabel.Name?.Trim();
            if (string.IsNullOrEmpty(name)) continue;
            if (!cache.TryGetValue(name, out var label))
            {
                label = new Label { Name = name };
                cache[name] = label;
                _db.Labels.Add(label);
            }
            result.Add(label);
        }
        return result;
    }

    private static Release ConstructNewReleaseEntity(CollectionFolderRelease item)
    {
        return new Release
        {
            DiscogsReleaseId = item.Id,
            Album = item.Release.Title,
            Year = item.Release.Year,
        };
    }

    public async Task SyncCollectionInBackground(CancellationToken ct)
    {
        var usersWithDiscogsUsernames = await _db.Users.Where(x => x.DiscogsUsername != null)
            .ToListAsync(ct);

        var artistCache = await _db.Artists.ToDictionaryAsync(a => a.Name, cancellationToken: ct);
        var labelCache  = await _db.Labels.ToDictionaryAsync(l => l.Name, cancellationToken: ct);

        foreach (var user in usersWithDiscogsUsernames)
        {
            if (user.DiscogsUsername.IsNullOrEmpty())
                continue;

            var seenIds = new HashSet<int>();
            var response = await _discogsApiClient.GetCollectionFolderReleases(user.DiscogsUsername!, 0, cancellationToken: ct);

            foreach (var item in response!.Releases)
            {
                await UpsertCollectionItem(user.Id, item, seenIds, artistCache, labelCache);
            }

            // Be polite
            await Task.Delay(1100, ct);
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task DownloadMissingImages(CancellationToken ct)
    {
        var imagesWithoutData = await _db.DiscogsReleaseImages
            .Include(i => i.Release)
            .Where(i => i.CoverImage == null && i.CoverUrl != null)
            .ToListAsync(ct);

        foreach (var image in imagesWithoutData)
        {
            try
            {
                var bytes = await _http.GetByteArrayAsync(image.CoverUrl, ct);
                image.CoverImage = bytes;

                // Save after each one so progress isn't lost on crash
                await _db.SaveChangesAsync(ct);

                _logger.LogInformation("Downloaded image for album {0} with release id {1}", image.Release.Album, image.DiscogsReleaseId);

                // Be polite
                await Task.Delay(1100, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to download image for album {0} with release id {1}", image.Release.Album, image.DiscogsReleaseId);
            }
        }
    }
}
