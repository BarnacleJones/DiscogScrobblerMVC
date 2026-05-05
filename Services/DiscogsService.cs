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

        while (hasMore)
        {
            var (pagination, releases) = await _discogsApiClient.GetCollectionFolderReleases(userId, 0);

            foreach (var item in releases)
            {
                var release = ConstructNewReleaseEntity(userId, item);

                var existing = await _db.Releases
                    .FirstOrDefaultAsync(r => r.DiscogsReleaseId == item.Id && r.UserId == userId);
                
                if (existing is null)
                    _db.Releases.Add(release);
                else
                    // Update cover URL in case it changed (Discogs CDN URLs rotate)
                    existing.CoverUrl = item.Release.CoverImageUrl;
            }

            await _db.SaveChangesAsync();

            var pages = pagination.TotalPages;
            hasMore = page < pages;
            page++;
        }

        _logger.LogInformation("Discogs sync complete for {Username}", discogsUsername);
    }

    private static Release ConstructNewReleaseEntity(string userId, CollectionFolderRelease item)
    {
        var releaseInformation = item.Release;
        var discogsId = item.Id;
        var addedAt = item.AddedAt;

        // Primary image
        var coverUrl = releaseInformation.CoverImageUrl;
        // Labels → take the first todo: maybe a joining table if this is annoying
        var label = releaseInformation.Labels.FirstOrDefault()?.Name;

        // Formats → take the first todo: maybe a joining table if this is annoying
        var format = releaseInformation.Formats.FirstOrDefault()?.Name;

        // Artist — join multiple todo: maybe a joining table if this is annoying
        var artistName = string.Join(", ", releaseInformation.Artists);

        return new Release
        {
            DiscogsReleaseId = discogsId,
            Artist = artistName,
            Album = releaseInformation.Title,
            Year = releaseInformation.Year,
            CoverUrl = coverUrl,
            Format = format,
            RecordLabel = label,
            DateAdded = addedAt,
            UserId = userId,
        };
    }

    public async Task SyncCollectionInBackground(CancellationToken ct)
    {
        var usersWithDiscogsUsernames = await _db.Users.Where(x => x.DiscogsUsername != null)
            .ToListAsync(ct);

        foreach (var user in usersWithDiscogsUsernames)
        {
            if( user.DiscogsUsername.IsNullOrEmpty())
                continue;
            
            var response = await _discogsApiClient.GetCollectionFolderReleases(user.DiscogsUsername!, 0, cancellationToken: ct);
            
            foreach (var item in response!.Releases)
            {
                var release = await _db.Releases.FindAsync([item.Id], ct);

                if (release is null)
                {
                    _db.Releases.Add(ConstructNewReleaseEntity(user.Id, item));
                }
                // else if (check other changed fields too, but remember they have user ids against them)
                // {
                //     release.Title = item.Title;
                // }
            }
            
            // Be polite 
            await Task.Delay(1100, ct);
        }
        
        await _db.SaveChangesAsync(ct);
    }
    
    public async Task DownloadMissingImages(CancellationToken ct)
    {
        var releasesWithoutImages = await _db.Releases
            .Where(r => r.CoverImage == null && r.CoverUrl != null)
            .ToListAsync(ct);

        foreach (var release in releasesWithoutImages)
        {
            try
            {
                var bytes = await _http.GetByteArrayAsync(release.CoverUrl, ct);
                release.CoverImage = bytes;

                // Save after each one so progress isn't lost on crash
                await _db.SaveChangesAsync(ct);

                _logger.LogInformation("Downloaded image for album {0} with release id {1}", release.Album, release.DiscogsReleaseId);

                // Be polite 
                await Task.Delay(1100, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to download image for album {0} with release id {1}", release.Album, release.DiscogsReleaseId);
            }
        }
    }
}