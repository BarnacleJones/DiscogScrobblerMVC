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

        var seenIds = new HashSet<int>();

        while (hasMore)
        {
            var (pagination, releases) = await _discogsApiClient.GetCollectionFolderReleases(userId, 0);

            foreach (var item in releases)
            {
                await UpsertCollectionItem(userId, item, seenIds);
            }

            await _db.SaveChangesAsync();

            var pages = pagination.TotalPages;
            hasMore = page < pages;
            page++;
        }

        _logger.LogInformation("Discogs sync complete for {Username}", discogsUsername);
    }

    private async Task UpsertCollectionItem(string userId, CollectionFolderRelease item, HashSet<int> seenIds)
    {
        // Duplicate DiscogsReleaseId within a batch (e.g. same release in two folders) — skip.
        if (!seenIds.Add(item.Id))
            return;

        var existingRelease = await _db.Releases
            .Include(r => r.Images)
            .FirstOrDefaultAsync(r => r.DiscogsReleaseId == item.Id);

        if (existingRelease is null)
        {
            _db.Releases.Add(ConstructNewReleaseEntity(item));
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

    private static Release ConstructNewReleaseEntity(CollectionFolderRelease item)
    {
        var releaseInformation = item.Release;

        // Labels → take the first todo: maybe a joining table if this is annoying
        var label = releaseInformation.Labels.FirstOrDefault()?.Name;

        // Formats → take the first todo: maybe a joining table if this is annoying
        var format = releaseInformation.Formats.FirstOrDefault()?.Name;

        // Artist — join multiple todo: maybe a joining table if this is annoying
        var artistName = string.Join(", ", releaseInformation.Artists);

        return new Release
        {
            DiscogsReleaseId = item.Id,
            Artist = artistName,
            Album = releaseInformation.Title,
            Year = releaseInformation.Year,
            Format = format,
            RecordLabel = label,
        };
    }

    public async Task SyncCollectionInBackground(CancellationToken ct)
    {
        var usersWithDiscogsUsernames = await _db.Users.Where(x => x.DiscogsUsername != null)
            .ToListAsync(ct);

        foreach (var user in usersWithDiscogsUsernames)
        {
            if (user.DiscogsUsername.IsNullOrEmpty())
                continue;

            var response = await _discogsApiClient.GetCollectionFolderReleases(user.DiscogsUsername!, 0, cancellationToken: ct);

            var seenIds = new HashSet<int>();
            foreach (var item in response!.Releases)
            {
                await UpsertCollectionItem(user.Id, item, seenIds);
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
