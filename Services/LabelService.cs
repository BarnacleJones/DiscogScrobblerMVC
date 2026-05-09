using DiscogsApiClient;
using DiscogScrobblerMVC.Data;
using DiscogScrobblerMVC.Data.Entities;
using DiscogScrobblerMVC.Models;
using DiscogScrobblerMVC.Services.Caching;
using DiscogScrobblerMVC.Services.Discogs;
using DiscogScrobblerMVC.Services.Interfaces;
using DiscogScrobblerMVC.Services.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace DiscogScrobblerMVC.Services;

public class LabelService : ILabelService
{
    private static readonly MemoryCacheEntryOptions DetailCacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(7),
    };

    private readonly ApplicationDbContext _db;
    private readonly IDiscogsApiClient _discogsApiClient;
    private readonly IMemoryCache _memoryCache;
    private readonly IDiscogsMetadataRefreshQueue _metadataRefreshQueue;
    private readonly ILogger<LabelService> _logger;

    public LabelService(
        ApplicationDbContext db,
        IDiscogsApiClient discogsApiClient,
        IMemoryCache memoryCache,
        IDiscogsMetadataRefreshQueue metadataRefreshQueue,
        ILogger<LabelService> logger)
    {
        _db = db;
        _discogsApiClient = discogsApiClient;
        _memoryCache = memoryCache;
        _metadataRefreshQueue = metadataRefreshQueue;
        _logger = logger;
    }

    public async Task<LabelViewModel?> GetLabel(int id, string userId, CancellationToken cancellationToken = default)
    {
        var discogsCoverSubfolderName =
            await DiscogsCoverSubfolder.TryGetNameForSignedInUserAsync(_db, userId, cancellationToken);

        var label = await _db.Labels.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (label is null)
            return null;

        var profile = label.DiscogsProfile;
        var imageUrl = label.DiscogsImageUrl;

        var needProfile = string.IsNullOrWhiteSpace(profile);
        var needImage = string.IsNullOrWhiteSpace(imageUrl);

        if (label.DiscogsLabelId.HasValue)
        {
            var discogsId = label.DiscogsLabelId.Value;
            var cacheKey = DiscogsMemoryCacheKeys.LabelDetails(discogsId);

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
                    var details = await _discogsApiClient.GetLabel(discogsId);
                    if (needProfile)
                        profile = details.Profile;

                    var pickedImage = DiscogsApiImages.PrimaryOrFirstUri(details.Images);
                    if (needImage)
                        imageUrl = pickedImage;

                    if (needProfile)
                        label.DiscogsProfile = profile;
                    if (needImage)
                        label.DiscogsImageUrl = imageUrl;
                    label.DiscogsDetailsFetchedAt = DateTimeOffset.UtcNow;
                    await _db.SaveChangesAsync(cancellationToken);

                    _memoryCache.Set(cacheKey, new DiscogsEntityDetailCacheEntry(profile, imageUrl), DetailCacheOptions);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Failed to fetch Discogs label details for {Name} ({DiscogsLabelId})", label.Name,
                        label.DiscogsLabelId);
                }
            }
        }

        if ((string.IsNullOrWhiteSpace(label.LocalImageFilename) || string.IsNullOrWhiteSpace(label.LocalThumbnailFilename))
            && !string.IsNullOrWhiteSpace(imageUrl))
        {
            var enqueued = _metadataRefreshQueue.EnqueueRefreshAllArtistLabelDetails();
            if (!enqueued)
                _logger.LogWarning("Could not enqueue artist/label image refresh after viewing label {LabelId}.", label.Id);
        }

        var collectionReleases = await _db.Releases.AsNoTracking()
            .Where(x => x.Labels.Any(y => y.Id == id) && x.UserAssociations.Any(y => y.UserId == userId))
            .Select(x => new ReleaseCardQueryResult(
                x.DiscogsReleaseId,
                x.Album,
                x.Year,
                x.Images!.LocalThumbnailFilename,
                x.Images!.LocalImageFilename,
                x.Images!.CoverUrl))
            .ToListAsync(cancellationToken);

        return ToViewModel(label, profile, imageUrl, discogsCoverSubfolderName, collectionReleases);
    }

    private static LabelViewModel ToViewModel(
        Label label,
        string? profile,
        string? imageUrl,
        string? discogsCoverSubfolderName,
        IEnumerable<ReleaseCardQueryResult> collectionReleases) =>
        new()
        {
            Id = label.Id,
            Name = label.Name,
            Profile = profile,
            ImageUrl = CoverImageUrlResolver.ResolveLabelProfileImageForGrid(
                label.LocalThumbnailFilename,
                label.LocalImageFilename,
                imageUrl),
            CollectionReleases = collectionReleases
                .Select(x => new CollectionReleaseCardViewModel(
                    x.ReleaseId,
                    x.Album,
                    x.Year,
                    CoverImageUrlResolver.ResolveReleaseCoverForGrid(
                        discogsCoverSubfolderName,
                        x.LocalThumbnailFilename,
                        x.LocalImageFilename,
                        x.CoverUrl)))
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
