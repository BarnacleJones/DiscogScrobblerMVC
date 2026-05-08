using DiscogsApiClient;
using DiscogsApiClient.Authentication;
using DiscogsApiClient.Contract.User.Collection;
using DiscogsApiClient.Exceptions;
using DiscogsApiClient.QueryParameters;
using DiscogScrobblerMVC.Data;
using DiscogScrobblerMVC.Data.Entities;
using DiscogScrobblerMVC.Services.Caching;
using DiscogScrobblerMVC.Services.Interfaces;
using DiscogScrobblerMVC.Services.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace DiscogScrobblerMVC.Services.Discogs;

public class DiscogsService : IDiscogsService
{
    private static readonly CollectionFolderReleaseSortQueryParameters AddedDateSortDesc = new(
        CollectionFolderReleaseSortQueryParameters.SortableProperty.AddedAt,
        SortOrder.Descending);

    private IDiscogsApiClient _discogsApiClient;
    private IDiscogsAuthenticationService _discogsAuthenticationService;
    private readonly HttpClient _http;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<DiscogsService> _logger;
    private readonly DiscogsExclusiveGate _discogsExclusiveGate;
    private readonly IMemoryCache _memoryCache;
    private readonly string _imageBasePath;
    private string? _writableBaseImageDirectory;
    private const int ThumbnailMaxWidth = 220;
    private const int ThumbnailQuality = 75;
    private static readonly TimeSpan DiscogsRequestDelay = TimeSpan.FromMilliseconds(1100);
    private static readonly TimeSpan ReleaseDetailsRequestDelay = TimeSpan.FromMilliseconds(1500);
    private static readonly TimeSpan RateLimitBackoff = TimeSpan.FromSeconds(61);

    public DiscogsService(HttpClient http, 
        IDiscogsApiClient discogsApiClient, 
        IDiscogsAuthenticationService discogsAuthenticationService, 
        ApplicationDbContext db, 
        ILogger<DiscogsService> logger,
        DiscogsExclusiveGate discogsExclusiveGate,
        IMemoryCache memoryCache,
        IWebHostEnvironment env,
        IOptions<AppSettings> settings)
    {
        _http = http;
        _discogsApiClient = discogsApiClient;
        _discogsAuthenticationService = discogsAuthenticationService;
        _db = db;
        _logger = logger;
        _discogsExclusiveGate = discogsExclusiveGate;
        _memoryCache = memoryCache;
        _imageBasePath = CoverStoragePathResolver.ResolveImageBasePath(env.ContentRootPath, settings.Value.ImageBasePath);
    }

    // Prefer configured/mounted folder (e.g. /app/images). If that cannot be created (read-only root), fall back to process temp.
    private string EnsureWritableBaseImageDirectory()
    {
        if (_writableBaseImageDirectory is not null)
            return _writableBaseImageDirectory;

        var fallbackDir = Path.Combine(Path.GetTempPath(), "DiscogScrobblerMVC", "images");

        foreach (var candidate in new[] { _imageBasePath, fallbackDir })
        {
            var isConfiguredPath =
                string.Equals(Path.GetFullPath(candidate), Path.GetFullPath(_imageBasePath), StringComparison.Ordinal);

            try
            {
                Directory.CreateDirectory(candidate);
                _writableBaseImageDirectory = candidate;
                if (!isConfiguredPath)
                {
                    _logger.LogWarning(
                        "Configured Discogs image path {Configured} was not writable; writing covers to {Actual} instead (see App:ImageBasePath / ./images Docker volume).",
                        _imageBasePath,
                        candidate);
                }

                return candidate;
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                if (isConfiguredPath)
                {
                    _logger.LogDebug(
                        ex,
                        "Discogs image directory {Configured} is not writable; trying Temp fallback ({Fallback}). Set App:ImageBasePath to a writable mount or chmod the volume.",
                        candidate,
                        fallbackDir);
                }
                else
                {
                    _logger.LogWarning(ex, "Cannot create Discogs fallback image directory at {Path}", candidate);
                }
            }
        }

        throw new InvalidOperationException(
            "No writable folder for Discogs cover images. Mount a writable path (see HOSTING.md) or set App:ImageBasePath.");
    }

    private bool TryEnsurePerDiscogsUserCoverDirectory(string ownerDiscogsUsername, out string absoluteCoverDirectory)
    {
        absoluteCoverDirectory = "";
        if (!CoverStoragePathResolver.TryGetDiscogsCoverSubfolderName(ownerDiscogsUsername, out var coverSubfolderName))
            return false;

        var sharedBaseDirectory = EnsureWritableBaseImageDirectory();
        absoluteCoverDirectory = Path.Combine(sharedBaseDirectory, coverSubfolderName);
        Directory.CreateDirectory(absoluteCoverDirectory);
        return true;
    }

    public void Authenticate(string token) =>
        _discogsAuthenticationService.AuthenticateWithPersonalAccessToken(token);

    private async Task<string?> TryGetPlainDiscogsTokenForUserAsync(
        string applicationUserId,
        CancellationToken cancellationToken)
    {
        var token = await _db.Users.AsNoTracking()
            .Where(u => u.Id == applicationUserId)
            .Select(u => u.DiscogsPersonalAccessToken)
            .FirstOrDefaultAsync(cancellationToken);

        return string.IsNullOrWhiteSpace(token) ? null : token.Trim();
    }

    public async Task SyncCollection(string discogsUsername, string userId)
    {
        var artistCache = await ArtistSyncCache.LoadAsync(_db, CancellationToken.None);
        var labelCache  = await LabelSyncCache.LoadAsync(_db, CancellationToken.None);

        await SyncDiscogsUserCollectionFoldersAsync(discogsUsername, userId, artistCache, labelCache, CancellationToken.None);
        await TryRefreshStoredCollectionValueAsync(discogsUsername, userId, CancellationToken.None);

        _logger.LogInformation("Discogs sync complete for {Username}", discogsUsername);
    }

    /// <summary>
    /// Fetches each collection folder except id 0 (Discogs &quot;All&quot; duplicates other folders—see CollectionFolder docs in DiscogsApiClient).
    /// </summary>
    private async Task SyncDiscogsUserCollectionFoldersAsync(
        string discogsUsername,
        string applicationUserId,
        ArtistSyncCache artistCache,
        LabelSyncCache labelCache,
        CancellationToken cancellationToken)
    {
        await _discogsExclusiveGate.WaitAsync(cancellationToken);
        try
        {
            await SyncDiscogsUserCollectionFoldersCoreAsync(
                discogsUsername, applicationUserId, artistCache, labelCache, cancellationToken);
        }
        finally
        {
            _discogsExclusiveGate.Release();
        }
    }

    private async Task SyncDiscogsUserCollectionFoldersCoreAsync(
        string discogsUsername,
        string applicationUserId,
        ArtistSyncCache artistCache,
        LabelSyncCache labelCache,
        CancellationToken cancellationToken)
    {
        var plainToken = await TryGetPlainDiscogsTokenForUserAsync(applicationUserId, cancellationToken);
        if (plainToken is not null)
            _discogsAuthenticationService.AuthenticateWithPersonalAccessToken(plainToken);
        else
        {
            DiscogsAuthenticationAnonymousHelper.ClearPersonalAccessStateForAnonymousRequests(
                _discogsAuthenticationService,
                _logger);
            _logger.LogInformation(
                "Discogs collection sync for user {UserId} has no saved token — requesting only what Discogs allows without owner auth (typically folder 0 if the collection is public).",
                applicationUserId);
        }

        var foldersResponse = await _discogsApiClient.GetCollectionFolders(discogsUsername, cancellationToken);
        var folderIds = foldersResponse.Folders.Where(x => x.Id != 0).Select(x => x.Id).Distinct().OrderBy(x => x).ToList();
        // If only the special "All" folder is present, sync it so empty collections do not regress.
        if (folderIds.Count == 0 && foldersResponse.Folders.Any(f => f.Id == 0))
            folderIds.Add(0);
        else if (folderIds.Count == 0)
        {
            _logger.LogWarning("No Discogs collection folders returned for {Username}", discogsUsername);
            return;
        }

        for (var i = 0; i < folderIds.Count; i++)
        {
            await SyncDiscogsFolderReleasesPagedAsync(
                discogsUsername,
                applicationUserId,
                folderIds[i],
                artistCache,
                labelCache,
                cancellationToken);

            if (i < folderIds.Count - 1)
                await Task.Delay(DiscogsRequestDelay, cancellationToken);
        }
    }

    /// <summary>
    /// Discogs caps at 100 items per page. Default folder sort without query params is arbitrary;
    /// we request <c>sort=added</c> / <c>sort_order=desc</c> when fetching (see Discogs docs).
    /// </summary>
    private async Task SyncDiscogsFolderReleasesPagedAsync(
        string discogsUsername,
        string applicationUserId,
        int folderId,
        ArtistSyncCache artistCache,
        LabelSyncCache labelCache,
        CancellationToken cancellationToken)
    {
        for (var page = 1; ; page++)
        {
            var pagination = new PaginationQueryParameters { Page = page, PageSize = 100 };
            var response = await _discogsApiClient.GetCollectionFolderReleases(
                discogsUsername,
                folderId,
                pagination,
                AddedDateSortDesc,
                cancellationToken);

            foreach (var item in response.Releases)
                await UpsertCollectionItem(applicationUserId, item, artistCache, labelCache, cancellationToken);

            await _db.SaveChangesAsync(cancellationToken);

            if (page >= response.Pagination.TotalPages)
                break;

            await Task.Delay(DiscogsRequestDelay, cancellationToken);
        }
    }

    /// <summary>
    /// Same Discogs release can appear multiple times in a folder sync (distinct instances/copies).
    /// The first upsert adds a tracked <see cref="Release"/> before <see cref="DbContext.SaveChangesAsync"/>;
    /// a bare database query misses that row, so resolve the change tracker first.
    /// </summary>
    private async Task<Release?> FindReleaseForCollectionUpsertAsync(int discogsReleaseId, CancellationToken cancellationToken)
    {
        var local = _db.Releases.Local.FirstOrDefault(r => r.DiscogsReleaseId == discogsReleaseId);
        if (local is not null)
            return local;

        return await _db.Releases
            .Include(r => r.Images)
            .FirstOrDefaultAsync(r => r.DiscogsReleaseId == discogsReleaseId, cancellationToken);
    }

    private async Task UpsertCollectionItem(
        string userId,
        CollectionFolderRelease item,
        ArtistSyncCache artistCache,
        LabelSyncCache labelCache,
        CancellationToken cancellationToken)
    {
        var existingRelease = await FindReleaseForCollectionUpsertAsync(item.Id, cancellationToken);

        var artists = ResolveArtists(item, artistCache);
        var labels  = ResolveLabels(item, labelCache);

        if (existingRelease is null)
        {
            var release = ConstructNewReleaseEntity(item);
            foreach (var a in artists) release.Artists.Add(a);
            foreach (var l in labels)  release.Labels.Add(l);

            // Cached artists/labels come from AsNoTracking queries; without attaching them first,
            // DbSet.Add(release) marks the whole graph as inserted and duplicates PKs on Artists/Labels.
            AttachExistingArtistsAndLabelsForReleaseGraph(artists, labels);

            _db.Releases.Add(release);
            _db.DiscogsReleaseImages.Add(new DiscogsReleaseImages
            {
                DiscogsReleaseId = item.Id,
                CoverUrl = NormalizeDiscogsCoverUrl(item.Release.CoverImageUrl),
            });
        }
        else
        {
            if (existingRelease.Images is not null)
                existingRelease.Images.CoverUrl = NormalizeDiscogsCoverUrl(item.Release.CoverImageUrl);
            ApplyMasterIdFromBasicInfo(existingRelease, item);
        }

        var instanceId = item.InstanceId;

        var existingUserLink = await _db.DiscogsReleaseToUsers
            .FirstOrDefaultAsync(
                x => x.UserId == userId && x.DiscogsInstanceId == instanceId,
                cancellationToken);

        if (existingUserLink is null)
        {
            existingUserLink = await _db.DiscogsReleaseToUsers
                .FirstOrDefaultAsync(
                    x => x.UserId == userId && x.DiscogsReleaseId == item.Id && x.DiscogsInstanceId == null,
                    cancellationToken);
        }

        if (existingUserLink is null)
        {
            _db.DiscogsReleaseToUsers.Add(new DiscogsReleaseToUser
            {
                DiscogsReleaseId = item.Id,
                UserId           = userId,
                DiscogsInstanceId = instanceId,
                DiscogsFolderId  = item.FolderId != 0 ? item.FolderId : (int?)null,
                DateAdded        = item.AddedAt,
            });
        }
        else
        {
            existingUserLink.DiscogsInstanceId = instanceId;
            existingUserLink.DiscogsFolderId   = item.FolderId != 0 ? item.FolderId : existingUserLink.DiscogsFolderId;
            existingUserLink.DiscogsReleaseId  = item.Id;
            if (existingUserLink.DateAdded != item.AddedAt)
                existingUserLink.DateAdded = item.AddedAt;
        }
    }

    private void AttachExistingArtistsAndLabelsForReleaseGraph(IEnumerable<Artist> artists, IEnumerable<Label> labels)
    {
        foreach (var a in artists)
        {
            if (a.Id != 0 && _db.Entry(a).State == EntityState.Detached)
                _db.Artists.Attach(a);
        }

        foreach (var l in labels)
        {
            if (l.Id != 0 && _db.Entry(l).State == EntityState.Detached)
                _db.Labels.Attach(l);
        }
    }

    private static void ApplyMasterIdFromBasicInfo(Release? release, CollectionFolderRelease item)
    {
        var mid = NormalizeDiscogsMasterId(item.Release.MasterId);
        if (release is null || !mid.HasValue)
            return;
        release.DiscogsMasterId = mid;
    }

    private static int? NormalizeDiscogsMasterId(int masterIdFromApi) =>
        masterIdFromApi > 0 ? masterIdFromApi : null;

    /// <summary>
    /// Discogs often returns protocol-relative image URLs (<c>//i.discogs.com/...</c>); <see cref="HttpClient"/> needs an absolute URI.
    /// </summary>
    private static string? NormalizeDiscogsCoverUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;
        var t = url.Trim();
        if (t.StartsWith("//", StringComparison.Ordinal))
            t = "https:" + t;
        if (!Uri.TryCreate(t, UriKind.Absolute, out var u))
            return null;
        if (u.Scheme != Uri.UriSchemeHttp && u.Scheme != Uri.UriSchemeHttps)
            return null;
        return t;
    }

    private static bool TryResolveCoverHttpUri(string? raw, out Uri uri)
    {
        var normalized = NormalizeDiscogsCoverUrl(raw);
        if (normalized is null)
        {
            uri = null!;
            return false;
        }

        return Uri.TryCreate(normalized, UriKind.Absolute, out uri!);
    }

    private List<Artist> ResolveArtists(CollectionFolderRelease item, ArtistSyncCache cache)
    {
        var result = new List<Artist>();
        foreach (var apiArtist in item.Release.Artists)
        {
            var name = apiArtist.Name?.Trim();
            var apiId = apiArtist.Id;
            var hasDiscogsId = apiId > 0;

            Artist? artist = null;

            if (hasDiscogsId && cache.TryGetByDiscogsId(apiId, out artist))
            {
                result.Add(artist!);
                continue;
            }

            if (hasDiscogsId)
            {
                artist = _db.Artists.Local.FirstOrDefault(a => a.DiscogsArtistId == apiId);
                if (artist is not null)
                {
                    cache.Remember(artist);
                    result.Add(artist);
                    continue;
                }
            }

            if (!string.IsNullOrEmpty(name) && cache.TryGetByName(name, out artist))
            {
                var matchByName = artist!;
                if (hasDiscogsId && (matchByName.DiscogsArtistId is null || matchByName.DiscogsArtistId <= 0))
                {
                    matchByName.DiscogsArtistId = apiId;
                    cache.RememberDiscogsIdMapping(matchByName, apiId);
                }
                cache.Remember(matchByName);
                result.Add(matchByName);
                continue;
            }

            if (string.IsNullOrEmpty(name))
            {
                if (!hasDiscogsId)
                    continue;
                name = $"Artist #{apiId}";
            }

            artist = new Artist { Name = name, DiscogsArtistId = hasDiscogsId ? apiId : null };
            cache.Remember(artist);
            _db.Artists.Add(artist);
            result.Add(artist);
        }

        return result;
    }

    private List<Label> ResolveLabels(CollectionFolderRelease item, LabelSyncCache cache)
    {
        var result = new List<Label>();
        foreach (var apiLabel in item.Release.Labels)
        {
            var name = apiLabel.Name?.Trim();
            var apiId = apiLabel.Id;
            var hasDiscogsId = apiId > 0;

            Label? label = null;

            if (hasDiscogsId && cache.TryGetByDiscogsId(apiId, out label))
            {
                result.Add(label!);
                continue;
            }

            if (hasDiscogsId)
            {
                label = _db.Labels.Local.FirstOrDefault(l => l.DiscogsLabelId == apiId);
                if (label is not null)
                {
                    cache.Remember(label);
                    result.Add(label);
                    continue;
                }
            }

            if (!string.IsNullOrEmpty(name) && cache.TryGetByName(name, out label))
            {
                var matchByName = label!;
                if (hasDiscogsId && (matchByName.DiscogsLabelId is null || matchByName.DiscogsLabelId <= 0))
                {
                    matchByName.DiscogsLabelId = apiId;
                    cache.RememberDiscogsIdMapping(matchByName, apiId);
                }
                cache.Remember(matchByName);
                result.Add(matchByName);
                continue;
            }

            if (string.IsNullOrEmpty(name))
            {
                if (!hasDiscogsId)
                    continue;
                name = $"Label #{apiId}";
            }

            label = new Label { Name = name, DiscogsLabelId = hasDiscogsId ? apiId : null };
            cache.Remember(label);
            _db.Labels.Add(label);
            result.Add(label);
        }

        return result;
    }

    private static Release ConstructNewReleaseEntity(CollectionFolderRelease item)
    {
        return new Release
        {
            DiscogsReleaseId = item.Id,
            Album             = item.Release.Title,
            Year              = item.Release.Year > 0 ? item.Release.Year : (int?)null,
            DiscogsMasterId   = NormalizeDiscogsMasterId(item.Release.MasterId),
        };
    }

    private async Task TryRefreshStoredCollectionValueAsync(
        string discogsUsername,
        string applicationUserId,
        CancellationToken cancellationToken)
    {
        try
        {
            await _discogsExclusiveGate.WaitAsync(cancellationToken);
            try
            {
                var plainToken = await TryGetPlainDiscogsTokenForUserAsync(applicationUserId, cancellationToken);
                if (plainToken is null)
                {
                    _logger.LogInformation(
                        "Skipping Discogs collection value refresh: no token for user {UserId}",
                        applicationUserId);
                    return;
                }

                _discogsAuthenticationService.AuthenticateWithPersonalAccessToken(plainToken);
                var value = await _discogsApiClient.GetCollectionValue(discogsUsername, cancellationToken);
                var userRow = await _db.Users.FirstOrDefaultAsync(u => u.Id == applicationUserId, cancellationToken);
                if (userRow is null)
                    return;

                userRow.DiscogsCollectionValueMin     = value.Minimum;
                userRow.DiscogsCollectionValueMedian  = value.Median;
                userRow.DiscogsCollectionValueMax     = value.Maximum;
                userRow.DiscogsCollectionValueFetchedAt = DateTimeOffset.UtcNow;
                await _db.SaveChangesAsync(cancellationToken);
            }
            finally
            {
                _discogsExclusiveGate.Release();
            }

            await Task.Delay(DiscogsRequestDelay, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not refresh Discogs collection value for {Username} ({UserId})", discogsUsername,
                applicationUserId);
        }
    }

    public async Task SyncCollectionInBackground(CancellationToken ct)
    {
        var usersWithDiscogs = await _db.Users
            .Where(x => x.DiscogsUsername != null)
            .ToListAsync(ct);

        _logger.LogInformation(
            "Starting scheduled Discogs collection sync for {UserCount} users.",
            usersWithDiscogs.Count);

        var artistCache = await ArtistSyncCache.LoadAsync(_db, ct);
        var labelCache  = await LabelSyncCache.LoadAsync(_db, ct);

        foreach (var user in usersWithDiscogs)
        {
            if (string.IsNullOrWhiteSpace(user.DiscogsUsername))
                continue;

            await SyncDiscogsUserCollectionFoldersAsync(user.DiscogsUsername!, user.Id, artistCache, labelCache, ct);
            await TryRefreshStoredCollectionValueAsync(user.DiscogsUsername!, user.Id, ct);

            await Task.Delay(DiscogsRequestDelay, ct);
        }

        _logger.LogInformation(
            "Scheduled Discogs collection sync complete for {UserCount} users.",
            usersWithDiscogs.Count);
    }

    public async Task SyncUserInBackground(string applicationUserId, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == applicationUserId, ct);
        if (user is null)
        {
            _logger.LogWarning("Sync requested for missing user {UserId}", applicationUserId);
            return;
        }

        if (string.IsNullOrWhiteSpace(user.DiscogsUsername))
        {
            _logger.LogInformation("Sync requested but Discogs username is empty for {UserId}", applicationUserId);
            return;
        }

        var artistCache = await ArtistSyncCache.LoadAsync(_db, ct);
        var labelCache  = await LabelSyncCache.LoadAsync(_db, ct);

        await SyncDiscogsUserCollectionFoldersAsync(user.DiscogsUsername!, user.Id, artistCache, labelCache, ct);
        await TryRefreshStoredCollectionValueAsync(user.DiscogsUsername!, user.Id, ct);

        _logger.LogInformation("Discogs collection sync complete for {Username} ({UserId})", user.DiscogsUsername, user.Id);

        await Task.Delay(DiscogsRequestDelay, ct);
    }

    public async Task SyncUserFullInBackground(string applicationUserId, CancellationToken ct)
    {
        await SyncUserInBackground(applicationUserId, ct);
        await SyncReleaseDetails(ct);
        await DownloadMissingImages(ct);
    }

    public async Task DownloadMissingImages(CancellationToken ct)
    {
        await _discogsExclusiveGate.WaitAsync(ct);
        try
        {
            await DownloadMissingImagesCore(ct);
        }
        finally
        {
            _discogsExclusiveGate.Release();
        }
    }

    private async Task DownloadMissingImagesCore(CancellationToken ct)
    {
        string baseImageDir;
        try
        {
            baseImageDir = EnsureWritableBaseImageDirectory();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Skipping Discogs cover downloads (no writable image directory).");
            return;
        }

        var coverRowsForOwnersWithDiscogsUsername = await (
            from releaseCover in _db.DiscogsReleaseImages
            where releaseCover.CoverUrl != null
                  || releaseCover.LocalImageFilename != null
                  || releaseCover.LocalThumbnailFilename != null
            from assoc in releaseCover.Release.UserAssociations
            join u in _db.Users on assoc.UserId equals u.Id
            where u.DiscogsUsername != null && !string.IsNullOrWhiteSpace(u.DiscogsUsername)
            select new
            {
                Cover = releaseCover,
                OwnerDiscogsUsername = u.DiscogsUsername!.Trim(),
                AlbumName = releaseCover.Release.Album,
            }).ToListAsync(ct);

        var distinctDownloadTargets = coverRowsForOwnersWithDiscogsUsername
            .GroupBy(x => new { x.Cover.Id, x.OwnerDiscogsUsername })
            .Select(g => g.First())
            .ToList();

        _logger.LogInformation(
            "Checking {Count} release cover download targets (per Discogs collection owner).",
            distinctDownloadTargets.Count);

        var preparedCount = 0;
        var failedCount = 0;

        foreach (var target in distinctDownloadTargets)
        {
            if (!TryEnsurePerDiscogsUserCoverDirectory(target.OwnerDiscogsUsername, out var perUserCoverDirectory))
                continue;

            var releaseCover = target.Cover;
            try
            {
                var fullImagePath = string.IsNullOrWhiteSpace(releaseCover.LocalImageFilename)
                    ? null
                    : Path.Combine(perUserCoverDirectory, releaseCover.LocalImageFilename);
                var thumbImagePath = string.IsNullOrWhiteSpace(releaseCover.LocalThumbnailFilename)
                    ? null
                    : Path.Combine(perUserCoverDirectory, releaseCover.LocalThumbnailFilename);

                var needsFullImage = string.IsNullOrWhiteSpace(fullImagePath) || !File.Exists(fullImagePath);
                var needsThumbnail = string.IsNullOrWhiteSpace(thumbImagePath) || !File.Exists(thumbImagePath);
                if (!needsFullImage && !needsThumbnail)
                    continue;

                byte[]? fullSizeBytes = null;

                if (needsFullImage)
                {
                    if (!TryResolveCoverHttpUri(releaseCover.CoverUrl, out var coverUri))
                    {
                        _logger.LogWarning(
                            "Skipping cover download for release {ReleaseId}: invalid or non-http(s) CoverUrl {Url}",
                            releaseCover.DiscogsReleaseId,
                            releaseCover.CoverUrl);
                        continue;
                    }

                    fullSizeBytes = await _http.GetByteArrayAsync(coverUri, ct);
                    var filename = $"{releaseCover.DiscogsReleaseId}.jpg";
                    var path = Path.Combine(perUserCoverDirectory, filename);
                    await File.WriteAllBytesAsync(path, fullSizeBytes, ct);

                    releaseCover.LocalImageFilename = filename;
                    fullImagePath = path;
                }

                if (needsThumbnail)
                {
                    if (string.IsNullOrWhiteSpace(fullImagePath) || !File.Exists(fullImagePath))
                    {
                        _logger.LogWarning(
                            "Cannot generate thumbnail for release {ReleaseId}: full-size local image is unavailable.",
                            releaseCover.DiscogsReleaseId);
                        continue;
                    }

                    fullSizeBytes ??= await ReadExistingReleaseCoverBytesAsync(
                        perUserCoverDirectory, releaseCover.LocalImageFilename!, ct);
                    var thumbFilename = $"{releaseCover.DiscogsReleaseId}-thumb.jpg";
                    var thumbPath = Path.Combine(perUserCoverDirectory, thumbFilename);
                    await WriteThumbnailAsync(fullSizeBytes, thumbPath, ct);
                    releaseCover.LocalThumbnailFilename = thumbFilename;
                }

                await _db.SaveChangesAsync(ct);

                _logger.LogInformation(
                    "Prepared local cover assets for album {0} with release id {1}",
                    target.AlbumName,
                    releaseCover.DiscogsReleaseId);
                preparedCount++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to prepare local cover assets for album {0} with release id {1}",
                    target.AlbumName,
                    releaseCover.DiscogsReleaseId);
                failedCount++;
            }
            finally
            {
                await Task.Delay(DiscogsRequestDelay, ct);
            }
        }

        await DownloadMissingArtistImagesCore(baseImageDir, ct);
        await DownloadMissingLabelImagesCore(baseImageDir, ct);

        _logger.LogInformation(
            "Release cover asset checks complete for all Discogs owners. Prepared {PreparedCount}; failed {FailedCount}.",
            preparedCount,
            failedCount);
    }

    private async Task DownloadMissingArtistImagesCore(string catalogImagesRootDirectory, CancellationToken ct)
    {
        var artistProfilesDirectory =
            Path.Combine(catalogImagesRootDirectory, CoverStoragePathResolver.SharedArtistProfileSubfolder);
        Directory.CreateDirectory(artistProfilesDirectory);

        var imageCandidates = await _db.Artists
            .Where(x => x.DiscogsImageUrl != null || x.LocalImageFilename != null || x.LocalThumbnailFilename != null)
            .Select(x => new
            {
                Artist = x,
                x.Name,
            })
            .ToListAsync(ct);

        _logger.LogInformation(
            "Checking {Count} artist image records for missing local assets.",
            imageCandidates.Count);

        var preparedCount = 0;
        var failedCount = 0;

        foreach (var item in imageCandidates)
        {
            var artist = item.Artist;
            try
            {
                var fullImagePath = string.IsNullOrWhiteSpace(artist.LocalImageFilename)
                    ? null
                    : Path.Combine(artistProfilesDirectory, artist.LocalImageFilename);
                var thumbImagePath = string.IsNullOrWhiteSpace(artist.LocalThumbnailFilename)
                    ? null
                    : Path.Combine(artistProfilesDirectory, artist.LocalThumbnailFilename);

                var needsFullImage = string.IsNullOrWhiteSpace(fullImagePath) || !File.Exists(fullImagePath);
                var needsThumbnail = string.IsNullOrWhiteSpace(thumbImagePath) || !File.Exists(thumbImagePath);
                if (!needsFullImage && !needsThumbnail)
                    continue;

                byte[]? fullSizeBytes = null;
                var imageKey = artist.DiscogsArtistId ?? artist.Id;

                if (needsFullImage)
                {
                    if (!TryResolveCoverHttpUri(artist.DiscogsImageUrl, out var imageUri))
                    {
                        _logger.LogWarning(
                            "Skipping artist image download for artist {ArtistId}: invalid or non-http(s) DiscogsImageUrl {Url}",
                            artist.Id,
                            artist.DiscogsImageUrl);
                        continue;
                    }

                    fullSizeBytes = await _http.GetByteArrayAsync(imageUri, ct);
                    var filename = $"artist-{imageKey}.jpg";
                    var path = Path.Combine(artistProfilesDirectory, filename);
                    await File.WriteAllBytesAsync(path, fullSizeBytes, ct);

                    artist.LocalImageFilename = filename;
                    fullImagePath = path;
                }

                if (needsThumbnail)
                {
                    if (string.IsNullOrWhiteSpace(fullImagePath) || !File.Exists(fullImagePath))
                    {
                        _logger.LogWarning(
                            "Cannot generate thumbnail for artist {ArtistId}: full-size local image is unavailable.",
                            artist.Id);
                        continue;
                    }

                    fullSizeBytes ??= await ReadExistingCatalogImageBytesAsync(
                        artistProfilesDirectory,
                        artist.LocalImageFilename!,
                        ct);
                    var thumbFilename = $"artist-{imageKey}-thumb.jpg";
                    var thumbPath = Path.Combine(artistProfilesDirectory, thumbFilename);
                    await WriteThumbnailAsync(fullSizeBytes, thumbPath, ct);
                    artist.LocalThumbnailFilename = thumbFilename;
                }

                await _db.SaveChangesAsync(ct);
                _logger.LogInformation("Prepared local artist image assets for {ArtistName} (artist id {ArtistId})", item.Name, artist.Id);
                preparedCount++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to prepare local artist image assets for {ArtistName} (artist id {ArtistId})", item.Name, artist.Id);
                failedCount++;
            }
            finally
            {
                await Task.Delay(DiscogsRequestDelay, ct);
            }
        }

        _logger.LogInformation(
            "Artist image asset check complete. Prepared {PreparedCount}; failed {FailedCount}.",
            preparedCount,
            failedCount);
    }

    private async Task DownloadMissingLabelImagesCore(string catalogImagesRootDirectory, CancellationToken ct)
    {
        var labelProfilesDirectory =
            Path.Combine(catalogImagesRootDirectory, CoverStoragePathResolver.SharedLabelProfileSubfolder);
        Directory.CreateDirectory(labelProfilesDirectory);

        var imageCandidates = await _db.Labels
            .Where(x => x.DiscogsImageUrl != null || x.LocalImageFilename != null || x.LocalThumbnailFilename != null)
            .Select(x => new
            {
                Label = x,
                x.Name,
            })
            .ToListAsync(ct);

        _logger.LogInformation(
            "Checking {Count} label image records for missing local assets.",
            imageCandidates.Count);

        var preparedCount = 0;
        var failedCount = 0;

        foreach (var item in imageCandidates)
        {
            var label = item.Label;
            try
            {
                var fullImagePath = string.IsNullOrWhiteSpace(label.LocalImageFilename)
                    ? null
                    : Path.Combine(labelProfilesDirectory, label.LocalImageFilename);
                var thumbImagePath = string.IsNullOrWhiteSpace(label.LocalThumbnailFilename)
                    ? null
                    : Path.Combine(labelProfilesDirectory, label.LocalThumbnailFilename);

                var needsFullImage = string.IsNullOrWhiteSpace(fullImagePath) || !File.Exists(fullImagePath);
                var needsThumbnail = string.IsNullOrWhiteSpace(thumbImagePath) || !File.Exists(thumbImagePath);
                if (!needsFullImage && !needsThumbnail)
                    continue;

                byte[]? fullSizeBytes = null;
                var imageKey = label.DiscogsLabelId ?? label.Id;

                if (needsFullImage)
                {
                    if (!TryResolveCoverHttpUri(label.DiscogsImageUrl, out var imageUri))
                    {
                        _logger.LogWarning(
                            "Skipping label image download for label {LabelId}: invalid or non-http(s) DiscogsImageUrl {Url}",
                            label.Id,
                            label.DiscogsImageUrl);
                        continue;
                    }

                    fullSizeBytes = await _http.GetByteArrayAsync(imageUri, ct);
                    var filename = $"label-{imageKey}.jpg";
                    var path = Path.Combine(labelProfilesDirectory, filename);
                    await File.WriteAllBytesAsync(path, fullSizeBytes, ct);

                    label.LocalImageFilename = filename;
                    fullImagePath = path;
                }

                if (needsThumbnail)
                {
                    if (string.IsNullOrWhiteSpace(fullImagePath) || !File.Exists(fullImagePath))
                    {
                        _logger.LogWarning(
                            "Cannot generate thumbnail for label {LabelId}: full-size local image is unavailable.",
                            label.Id);
                        continue;
                    }

                    fullSizeBytes ??= await ReadExistingCatalogImageBytesAsync(
                        labelProfilesDirectory,
                        label.LocalImageFilename!,
                        ct);
                    var thumbFilename = $"label-{imageKey}-thumb.jpg";
                    var thumbPath = Path.Combine(labelProfilesDirectory, thumbFilename);
                    await WriteThumbnailAsync(fullSizeBytes, thumbPath, ct);
                    label.LocalThumbnailFilename = thumbFilename;
                }

                await _db.SaveChangesAsync(ct);
                _logger.LogInformation("Prepared local label image assets for {LabelName} (label id {LabelId})", item.Name, label.Id);
                preparedCount++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to prepare local label image assets for {LabelName} (label id {LabelId})", item.Name, label.Id);
                failedCount++;
            }
            finally
            {
                await Task.Delay(DiscogsRequestDelay, ct);
            }
        }

        _logger.LogInformation(
            "Label image asset check complete. Prepared {PreparedCount}; failed {FailedCount}.",
            preparedCount,
            failedCount);
    }

    private static async Task<byte[]> ReadExistingReleaseCoverBytesAsync(
        string perDiscogsUserCoverDirectory,
        string storedFileNameOnly,
        CancellationToken ct)
    {
        var pathUnderDiscogsUser = Path.Combine(perDiscogsUserCoverDirectory, storedFileNameOnly);
        if (!File.Exists(pathUnderDiscogsUser))
            throw new FileNotFoundException("Release cover file not found in per-discogs-user folder.", pathUnderDiscogsUser);

        return await File.ReadAllBytesAsync(pathUnderDiscogsUser, ct);
    }

    private static async Task<byte[]> ReadExistingCatalogImageBytesAsync(
        string catalogImagesRootDirectory,
        string storedFileNameOnly,
        CancellationToken ct)
    {
        var fullImagePath = Path.Combine(catalogImagesRootDirectory, storedFileNameOnly);
        return await File.ReadAllBytesAsync(fullImagePath, ct);
    }

    private static async Task WriteThumbnailAsync(byte[] sourceBytes, string outputPath, CancellationToken ct)
    {
        await using var sourceStream = new MemoryStream(sourceBytes);
        using var image = await Image.LoadAsync(sourceStream, ct);
        image.Mutate(x => x.Resize(new ResizeOptions
        {
            Mode = ResizeMode.Max,
            Size = new Size(ThumbnailMaxWidth, ThumbnailMaxWidth),
        }));

        var encoder = new JpegEncoder { Quality = ThumbnailQuality };
        await image.SaveAsJpegAsync(outputPath, encoder, ct);
    }

    public async Task SyncReleaseDetails(CancellationToken ct)
    {
        await _discogsExclusiveGate.WaitAsync(ct);
        try
        {
            await SyncReleaseDetailsCore(ct);
        }
        finally
        {
            _discogsExclusiveGate.Release();
        }
    }

    private async Task ReplaceReleaseGenresAndStylesAsync(
        int releaseId,
        IEnumerable<string>? genres,
        IEnumerable<string>? styles,
        CancellationToken ct)
    {
        await _db.ReleaseGenres
            .Where(x => x.ReleaseId == releaseId)
            .ExecuteDeleteAsync(ct);
        await _db.ReleaseStyles
            .Where(x => x.ReleaseId == releaseId)
            .ExecuteDeleteAsync(ct);

        if (genres is not null)
        {
            var requestedGenres = genres
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Select(trimmed => new { Trimmed = trimmed, Normalized = trimmed.ToLowerInvariant() })
                .DistinctBy(x => x.Normalized)
                .ToList();

            var requestedGenreKeys = requestedGenres.Select(x => x.Normalized).ToList();
            var existingGenres = await _db.Genres
                .Where(x => requestedGenreKeys.Contains(x.NormalizedName))
                .ToDictionaryAsync(g => g.NormalizedName, g => g, StringComparer.Ordinal, ct);

            foreach (var item in requestedGenres)
            {
                if (!existingGenres.TryGetValue(item.Normalized, out var genre))
                {
                    genre = new Genre { Name = item.Trimmed, NormalizedName = item.Normalized };
                    _db.Genres.Add(genre);
                    existingGenres[item.Normalized] = genre;
                }

                _db.ReleaseGenres.Add(new ReleaseGenre { ReleaseId = releaseId, Genre = genre });
            }
        }

        if (styles is null)
            return;

        var requestedStyles = styles
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Select(trimmed => new { Trimmed = trimmed, Normalized = trimmed.ToLowerInvariant() })
            .DistinctBy(x => x.Normalized)
            .ToList();

        var requestedStyleKeys = requestedStyles.Select(x => x.Normalized).ToList();
        var existingStyles = await _db.Styles
            .Where(x => requestedStyleKeys.Contains(x.NormalizedName))
            .ToDictionaryAsync(s => s.NormalizedName, s => s, StringComparer.Ordinal, ct);

        foreach (var item in requestedStyles)
        {
            if (!existingStyles.TryGetValue(item.Normalized, out var style))
            {
                style = new Style { Name = item.Trimmed, NormalizedName = item.Normalized };
                _db.Styles.Add(style);
                existingStyles[item.Normalized] = style;
            }

            _db.ReleaseStyles.Add(new ReleaseStyle { ReleaseId = releaseId, Style = style });
        }
    }

    private readonly record struct ReleaseDetailsSyncRow(int Id, int DiscogsReleaseId, string Album);

    private async Task SyncReleaseDetailsCore(CancellationToken ct)
    {
        var releasesWithoutDetails = await _db.Releases
            .AsNoTracking()
            .Where(x => !x.Tracks.Any())
            .Select(x => new ReleaseDetailsSyncRow(x.Id, x.DiscogsReleaseId, x.Album))
            .ToListAsync(ct);

        _logger.LogInformation(
            "Syncing Discogs details for {ReleaseCount} releases missing track details.",
            releasesWithoutDetails.Count);

        var syncedCount = 0;
        var failedCount = 0;

        foreach (var release in releasesWithoutDetails)
        {
            try
            {
                if (await _db.Tracks.AnyAsync(t => t.ReleaseId == release.Id, ct))
                {
                    _logger.LogInformation(
                        "Skipping details for {Album} ({DiscogsReleaseId}); tracks were already synced.",
                        release.Album,
                        release.DiscogsReleaseId);
                    continue;
                }

                var details = await _discogsApiClient.GetRelease(release.DiscogsReleaseId, ct);

                await using var transaction = await _db.Database.BeginTransactionAsync(ct);

                if (await _db.Tracks.AnyAsync(t => t.ReleaseId == release.Id, ct))
                {
                    _logger.LogInformation(
                        "Skipping details for {Album} ({DiscogsReleaseId}); tracks were already synced.",
                        release.Album,
                        release.DiscogsReleaseId);
                    await transaction.RollbackAsync(ct);
                    continue;
                }

                await ReplaceReleaseGenresAndStylesAsync(release.Id, details.Genres, details.Styles, ct);

                foreach (var t in details.Tracklist.Where(x => x.Type == "track"))
                {
                    var seconds = TrackDurationParser.TryParseSeconds(t.Duration);
                    _db.Tracks.Add(new Track
                    {
                        ReleaseId = release.Id,
                        Position = t.Position,
                        Title = t.Title,
                        Duration = t.Duration,
                        DurationSeconds = seconds is > 0 ? seconds : null,
                    });
                }

                await _db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);

                _logger.LogInformation("Synced details for {Album} ({DiscogsReleaseId})", release.Album, release.DiscogsReleaseId);
                syncedCount++;
            }
            catch (RateLimitExceededDiscogsException)
            {
                _logger.LogWarning("Discogs rate limited while syncing {Album} ({DiscogsReleaseId}); backing off.", release.Album,
                    release.DiscogsReleaseId);
                failedCount++;
                await Task.Delay(RateLimitBackoff, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to sync details for {Album} ({DiscogsReleaseId})", release.Album, release.DiscogsReleaseId);
                _db.ChangeTracker.Clear();
                failedCount++;
            }
            finally
            {
                // Be polite — failures used to skip the delay and hammer the API in a tight loop (e.g. after 429 responses).
                await Task.Delay(ReleaseDetailsRequestDelay, ct);
            }
        }

        _logger.LogInformation(
            "Discogs release detail sync complete. Synced {SyncedCount}; failed {FailedCount}.",
            syncedCount,
            failedCount);
    }

    public async Task RefreshAllArtistLabelDiscogsDetailsAsync(CancellationToken ct)
    {
        var artists = await _db.Artists.AsNoTracking()
            .Where(x => x.DiscogsArtistId != null)
            .Select(x => new ArtistMetadataRefreshRow(x.Id, x.DiscogsArtistId!.Value))
            .ToListAsync(ct);

        _logger.LogInformation(
            "Refreshing Discogs metadata for {ArtistCount} artists.",
            artists.Count);

        foreach (var artist in artists)
            await RefreshArtistDiscogsDetailsAsync(artist, ct);

        var labels = await _db.Labels.AsNoTracking()
            .Where(x => x.DiscogsLabelId != null)
            .Select(x => new LabelMetadataRefreshRow(x.Id, x.DiscogsLabelId!.Value))
            .ToListAsync(ct);

        _logger.LogInformation(
            "Refreshing Discogs metadata for {LabelCount} labels.",
            labels.Count);

        foreach (var label in labels)
            await RefreshLabelDiscogsDetailsAsync(label, ct);

        _logger.LogInformation(
            "Discogs artist/label metadata refresh complete for {ArtistCount} artists and {LabelCount} labels.",
            artists.Count,
            labels.Count);
    }

    private readonly record struct ArtistMetadataRefreshRow(int Id, int DiscogsArtistId);

    private readonly record struct LabelMetadataRefreshRow(int Id, int DiscogsLabelId);

    private async Task RefreshArtistDiscogsDetailsAsync(ArtistMetadataRefreshRow row, CancellationToken ct)
    {
        try
        {
            _memoryCache.Remove(DiscogsMemoryCacheKeys.ArtistDetails(row.DiscogsArtistId));

            await _discogsExclusiveGate.WaitAsync(ct);
            try
            {
                var artist = await _db.Artists.FirstAsync(a => a.Id == row.Id, ct);
                var details = await _discogsApiClient.GetArtist(row.DiscogsArtistId, ct);
                artist.DiscogsProfile = details.Profile;

                if (string.IsNullOrWhiteSpace(artist.DiscogsImageUrl))
                    artist.DiscogsImageUrl = DiscogsApiImages.PrimaryOrFirstUri(details.Images);

                artist.DiscogsDetailsFetchedAt = DateTimeOffset.UtcNow;
                await _db.SaveChangesAsync(ct);
            }
            finally
            {
                _discogsExclusiveGate.Release();
            }
        }
        catch (RateLimitExceededDiscogsException)
        {
            _logger.LogWarning(
                "Discogs rate limited during artist metadata refresh ({DiscogsArtistId}); backing off.",
                row.DiscogsArtistId);
            await Task.Delay(RateLimitBackoff, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed metadata refresh for artist id {ArtistId} (Discogs {DiscogsArtistId})",
                row.Id,
                row.DiscogsArtistId);
        }
        finally
        {
            await Task.Delay(DiscogsRequestDelay, ct);
        }
    }

    private async Task RefreshLabelDiscogsDetailsAsync(LabelMetadataRefreshRow row, CancellationToken ct)
    {
        try
        {
            _memoryCache.Remove(DiscogsMemoryCacheKeys.LabelDetails(row.DiscogsLabelId));

            await _discogsExclusiveGate.WaitAsync(ct);
            try
            {
                var label = await _db.Labels.FirstAsync(l => l.Id == row.Id, ct);
                var details = await _discogsApiClient.GetLabel(row.DiscogsLabelId, ct);
                label.DiscogsProfile = details.Profile;

                if (string.IsNullOrWhiteSpace(label.DiscogsImageUrl))
                    label.DiscogsImageUrl = DiscogsApiImages.PrimaryOrFirstUri(details.Images);

                label.DiscogsDetailsFetchedAt = DateTimeOffset.UtcNow;
                await _db.SaveChangesAsync(ct);
            }
            finally
            {
                _discogsExclusiveGate.Release();
            }
        }
        catch (RateLimitExceededDiscogsException)
        {
            _logger.LogWarning(
                "Discogs rate limited during label metadata refresh ({DiscogsLabelId}); backing off.",
                row.DiscogsLabelId);
            await Task.Delay(RateLimitBackoff, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed metadata refresh for label id {LabelId} (Discogs {DiscogsLabelId})",
                row.Id,
                row.DiscogsLabelId);
        }
        finally
        {
            await Task.Delay(DiscogsRequestDelay, ct);
        }
    }

    /// <summary>Lookup artists by Discogs id first, then display name — collection API name strings are not stable per id.</summary>
    private sealed class ArtistSyncCache
    {
        private readonly Dictionary<string, Artist> _byName;
        private readonly Dictionary<int, Artist> _byDiscogsId;

        public static async Task<ArtistSyncCache> LoadAsync(ApplicationDbContext db, CancellationToken ct)
        {
            var list = await db.Artists.AsNoTracking().ToListAsync(ct);
            var byName = new Dictionary<string, Artist>(StringComparer.Ordinal);
            foreach (var a in list)
            {
                if (!byName.ContainsKey(a.Name))
                    byName[a.Name] = a;
            }

            var byDiscogsId = new Dictionary<int, Artist>();
            foreach (var a in list)
            {
                if (a.DiscogsArtistId is not int aid || aid <= 0)
                    continue;
                if (!byDiscogsId.ContainsKey(aid))
                    byDiscogsId[aid] = a;
            }

            return new ArtistSyncCache(byName, byDiscogsId);
        }

        private ArtistSyncCache(Dictionary<string, Artist> byName, Dictionary<int, Artist> byDiscogsId)
        {
            _byName = byName;
            _byDiscogsId = byDiscogsId;
        }

        public bool TryGetByDiscogsId(int discogsArtistId, out Artist? artist) =>
            _byDiscogsId.TryGetValue(discogsArtistId, out artist);

        public bool TryGetByName(string name, out Artist? artist) =>
            _byName.TryGetValue(name, out artist);

        public void Remember(Artist artist)
        {
            if (!_byName.ContainsKey(artist.Name))
                _byName[artist.Name] = artist;
            if (artist.DiscogsArtistId is int did && did > 0 && !_byDiscogsId.ContainsKey(did))
                _byDiscogsId[did] = artist;
        }

        public void RememberDiscogsIdMapping(Artist artist, int discogsArtistId)
        {
            if (discogsArtistId <= 0 || _byDiscogsId.ContainsKey(discogsArtistId))
                return;
            _byDiscogsId[discogsArtistId] = artist;
        }
    }

    private sealed class LabelSyncCache
    {
        private readonly Dictionary<string, Label> _byName;
        private readonly Dictionary<int, Label> _byDiscogsId;

        public static async Task<LabelSyncCache> LoadAsync(ApplicationDbContext db, CancellationToken ct)
        {
            var list = await db.Labels.AsNoTracking().ToListAsync(ct);
            var byName = new Dictionary<string, Label>(StringComparer.Ordinal);
            foreach (var l in list)
            {
                if (!byName.ContainsKey(l.Name))
                    byName[l.Name] = l;
            }

            var byDiscogsId = new Dictionary<int, Label>();
            foreach (var l in list)
            {
                if (l.DiscogsLabelId is not int lid || lid <= 0)
                    continue;
                if (!byDiscogsId.ContainsKey(lid))
                    byDiscogsId[lid] = l;
            }

            return new LabelSyncCache(byName, byDiscogsId);
        }

        private LabelSyncCache(Dictionary<string, Label> byName, Dictionary<int, Label> byDiscogsId)
        {
            _byName = byName;
            _byDiscogsId = byDiscogsId;
        }

        public bool TryGetByDiscogsId(int discogsLabelId, out Label? label) =>
            _byDiscogsId.TryGetValue(discogsLabelId, out label);

        public bool TryGetByName(string name, out Label? label) =>
            _byName.TryGetValue(name, out label);

        public void Remember(Label label)
        {
            if (!_byName.ContainsKey(label.Name))
                _byName[label.Name] = label;
            if (label.DiscogsLabelId is int lid && lid > 0 && !_byDiscogsId.ContainsKey(lid))
                _byDiscogsId[lid] = label;
        }

        public void RememberDiscogsIdMapping(Label label, int discogsLabelId)
        {
            if (discogsLabelId <= 0 || _byDiscogsId.ContainsKey(discogsLabelId))
                return;
            _byDiscogsId[discogsLabelId] = label;
        }
    }
}
