using DiscogsApiClient;
using DiscogScrobblerMVC.Data;
using DiscogScrobblerMVC.Data.Entities;
using DiscogScrobblerMVC.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace DiscogScrobblerMVC.Services;

public class ArtistService : IArtistService
{
    private static readonly MemoryCacheEntryOptions DetailCacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(7),
    };

    private readonly ApplicationDbContext _db;
    private readonly IDiscogsApiClient _discogsApiClient;
    private readonly IMemoryCache _memoryCache;
    private readonly IDiscogsMetadataRefreshQueue _metadataRefreshQueue;
    private readonly ILogger<ArtistService> _logger;

    public ArtistService(
        ApplicationDbContext db,
        IDiscogsApiClient discogsApiClient,
        IMemoryCache memoryCache,
        IDiscogsMetadataRefreshQueue metadataRefreshQueue,
        ILogger<ArtistService> logger)
    {
        _db = db;
        _discogsApiClient = discogsApiClient;
        _memoryCache = memoryCache;
        _metadataRefreshQueue = metadataRefreshQueue;
        _logger = logger;
    }

    public async Task<ArtistViewModel?> GetArtist(int id, CancellationToken cancellationToken = default)
    {
        var artist = await _db.Artists.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        if (artist is null)
            return null;

        var profile = artist.DiscogsProfile;
        var imageUrl = artist.DiscogsImageUrl;

        var needProfile = string.IsNullOrWhiteSpace(profile);
        var needImage = string.IsNullOrWhiteSpace(imageUrl);

        if (artist.DiscogsArtistId.HasValue)
        {
            var discogsId = artist.DiscogsArtistId.Value;
            var cacheKey = DiscogsMemoryCacheKeys.ArtistDetails(discogsId);

            if ((needProfile || needImage)
                && _memoryCache.TryGetValue(cacheKey, out var raw)
                && raw is DiscogsEntityDetailCacheEntry cached)
            {
                if (needProfile && !string.IsNullOrWhiteSpace(cached.Profile))
                    profile = cached.Profile;
                if (needImage && !string.IsNullOrWhiteSpace(cached.ImageUrl))
                    imageUrl = cached.ImageUrl;
                needProfile = string.IsNullOrWhiteSpace(profile);
                needImage = string.IsNullOrWhiteSpace(imageUrl);
            }

            if (needProfile || needImage)
            {
                try
                {
                    var details = await _discogsApiClient.GetArtist(discogsId);
                    if (needProfile)
                        profile = details.Profile;

                    var pickedImage = DiscogsApiImages.PrimaryOrFirstUri(details.Images);
                    if (needImage)
                        imageUrl = pickedImage;

                    if (needProfile)
                        artist.DiscogsProfile = profile;
                    if (needImage)
                        artist.DiscogsImageUrl = imageUrl;
                    artist.DiscogsDetailsFetchedAt = DateTimeOffset.UtcNow;
                    await _db.SaveChangesAsync(cancellationToken);

                    _memoryCache.Set(cacheKey, new DiscogsEntityDetailCacheEntry(profile, imageUrl), DetailCacheOptions);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Failed to fetch Discogs artist details for {Name} ({DiscogsArtistId})", artist.Name,
                        artist.DiscogsArtistId);
                }
            }
        }

        if ((string.IsNullOrWhiteSpace(artist.LocalImageFilename) || string.IsNullOrWhiteSpace(artist.LocalThumbnailFilename))
            && !string.IsNullOrWhiteSpace(imageUrl))
        {
            var enqueued = _metadataRefreshQueue.EnqueueRefreshAllArtistLabelDetails();
            if (!enqueued)
                _logger.LogWarning("Could not enqueue artist/label image refresh after viewing artist {ArtistId}.", artist.Id);
        }

        var collectionReleases = await _db.Releases.AsNoTracking()
            .Where(x => x.Artists.Any(x => x.Id == id))
            .Select(x => new ReleaseCardQueryResult(
                x.DiscogsReleaseId,
                x.Album,
                x.Year,
                x.Images!.LocalThumbnailFilename,
                x.Images!.LocalImageFilename,
                x.Images!.CoverUrl))
            .ToListAsync(cancellationToken);

        return ToViewModel(artist, profile, imageUrl, collectionReleases);
    }

    private static ArtistViewModel ToViewModel(
        Artist artist,
        string? profile,
        string? imageUrl,
        IEnumerable<ReleaseCardQueryResult> collectionReleases) =>
        new()
        {
            Id = artist.Id,
            Name = artist.Name,
            Profile = profile,
            ImageUrl = CoverImageUrlResolver.ResolveForGrid(
                artist.LocalThumbnailFilename,
                artist.LocalImageFilename,
                imageUrl),
            CollectionReleases = collectionReleases
                .Select(x => new CollectionReleaseCardViewModel(
                    x.ReleaseId,
                    x.Album,
                    x.Year,
                    CoverImageUrlResolver.ResolveForGrid(x.LocalThumbnailFilename, x.LocalImageFilename, x.CoverUrl)))
                .OrderByDescending(x => x.Year)
                .ToList(),
        };

    private readonly record struct ReleaseCardQueryResult(
        int ReleaseId,
        string Album,
        int? Year,
        string? LocalThumbnailFilename,
        string? LocalImageFilename,
        string? CoverUrl);
}
