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

    private const int DiscogsCollectionFolderApiPageSize = 100;

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

    /// <summary>Use App:ImageBasePath when writable; otherwise fall back to temp (e.g. read-only container root).</summary>
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

    private async Task<string?> TryGetDiscogsPersonalAccessTokenForUserAsync(
        string applicationUserId,
        CancellationToken cancellationToken)
    {
        var token = await _db.Users.AsNoTracking()
            .Where(x => x.Id == applicationUserId)
            .Select(x => x.DiscogsPersonalAccessToken)
            .FirstOrDefaultAsync(cancellationToken);

        return string.IsNullOrWhiteSpace(token) ? null : token.Trim();
    }

    public async Task SyncCollection(string discogsUsername, string applicationUserId)
    {
        var artistCache = await ArtistSyncCache.LoadAsync(_db, CancellationToken.None);
        var labelCache  = await LabelSyncCache.LoadAsync(_db, CancellationToken.None);

        await SyncDiscogsUserCollectionFoldersAsync(discogsUsername, applicationUserId, artistCache, labelCache, CancellationToken.None);
        await TryRefreshStoredCollectionValueAsync(discogsUsername, applicationUserId, CancellationToken.None);

        _logger.LogInformation("Discogs sync complete for {Username}", discogsUsername);
    }

    /// <summary>PAT holder: sync each folder except id 0. No PAT: sync folder 0 only (public collection).</summary>
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
        var personalAccessToken = await TryGetDiscogsPersonalAccessTokenForUserAsync(applicationUserId, cancellationToken);
        List<int> folderIds;

        if (personalAccessToken is not null)
        {
            _discogsAuthenticationService.AuthenticateWithPersonalAccessToken(personalAccessToken);
            var foldersResponse = await _discogsApiClient.GetCollectionFolders(discogsUsername, cancellationToken);
            folderIds = foldersResponse.Folders.Where(x => x.Id != 0).Select(x => x.Id).Distinct().OrderBy(x => x).ToList();
            var hasAllFolderVirtual = foldersResponse.Folders.Any(x => x.Id == 0);
            var noFoldersExceptAllPlaceholder = folderIds.Count == 0;
            if (noFoldersExceptAllPlaceholder && hasAllFolderVirtual)
                folderIds.Add(0);
            else if (folderIds.Count == 0)
            {
                _logger.LogWarning("No Discogs collection folders returned for {Username}", discogsUsername);
                return;
            }
        }
        else
        {
            DiscogsAuthenticationAnonymousHelper.ClearPersonalAccessStateForAnonymousRequests(
                _discogsAuthenticationService,
                _logger);
            _logger.LogInformation(
                "Discogs collection sync for user {UserId} has no saved token — syncing folder 0 (All) via public endpoints only. Works if Discogs collection visibility is public; folder listing and non-public collections require a personal access token.",
                applicationUserId);
            folderIds = new List<int> { 0 };
        }

        try
        {
            for (var folderIndex = 0; folderIndex < folderIds.Count; folderIndex++)
            {
                await SyncDiscogsFolderReleasesPagedAsync(
                    discogsUsername,
                    applicationUserId,
                    folderIds[folderIndex],
                    artistCache,
                    labelCache,
                    cancellationToken);

                if (folderIndex < folderIds.Count - 1)
                    await Task.Delay(DiscogsRequestDelay, cancellationToken);
            }
        }
        catch (UnauthenticatedDiscogsException ex) when (personalAccessToken is null)
        {
            _logger.LogWarning(
                ex,
                "Discogs collection for user {UserId} ({Username}) rejected unauthenticated access (folder 0). Discogs collection is not public for the API, or visibility is restricted — save a personal access token in Settings.",
                applicationUserId,
                discogsUsername);
        }
    }

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
            var pagination = new PaginationQueryParameters { Page = page, PageSize = DiscogsCollectionFolderApiPageSize };
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

    /// <summary>Prefer the change-tracker so a release added earlier in this sync round is visible before SaveChanges.</summary>
    private async Task<Release?> FindReleaseForCollectionUpsertAsync(int discogsReleaseId, CancellationToken cancellationToken)
    {
        var localRelease =
            _db.Releases.Local.FirstOrDefault(x => x.DiscogsReleaseId == discogsReleaseId);
        if (localRelease is not null)
            return localRelease;

        return await _db.Releases
            .Include(x => x.Images)
            .FirstOrDefaultAsync(x => x.DiscogsReleaseId == discogsReleaseId, cancellationToken);
    }

    private async Task UpsertCollectionItem(
        string applicationUserId,
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
            foreach (var artist in artists) release.Artists.Add(artist);
            foreach (var label in labels)  release.Labels.Add(label);

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
                x => x.UserId == applicationUserId && x.DiscogsInstanceId == instanceId,
                cancellationToken);

        if (existingUserLink is null)
        {
            existingUserLink = await _db.DiscogsReleaseToUsers
                .FirstOrDefaultAsync(
                    x => x.UserId == applicationUserId && x.DiscogsReleaseId == item.Id && x.DiscogsInstanceId == null,
                    cancellationToken);
        }

        if (existingUserLink is null)
        {
            _db.DiscogsReleaseToUsers.Add(new DiscogsReleaseToUser
            {
                DiscogsReleaseId = item.Id,
                UserId           = applicationUserId,
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

    /// <summary>Detached cache rows must be attached before <c>Add(release)</c> or EF inserts duplicate artist/label PKs.</summary>
    private void AttachExistingArtistsAndLabelsForReleaseGraph(IEnumerable<Artist> artists, IEnumerable<Label> labels)
    {
        foreach (var artist in artists)
        {
            if (artist.Id != 0 && _db.Entry(artist).State == EntityState.Detached)
                _db.Artists.Attach(artist);
        }

        foreach (var label in labels)
        {
            if (label.Id != 0 && _db.Entry(label).State == EntityState.Detached)
                _db.Labels.Attach(label);
        }
    }

    private static void ApplyMasterIdFromBasicInfo(Release? release, CollectionFolderRelease item)
    {
        var normalizedMasterId = NormalizeDiscogsMasterId(item.Release.MasterId);
        if (release is null || !normalizedMasterId.HasValue)
            return;
        release.DiscogsMasterId = normalizedMasterId;
    }

    private static int? NormalizeDiscogsMasterId(int masterIdFromApi) =>
        masterIdFromApi > 0 ? masterIdFromApi : null;

    private static string? NormalizeDiscogsCoverUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;
        var trimmedUrl = url.Trim();
        if (trimmedUrl.StartsWith("//", StringComparison.Ordinal))
            trimmedUrl = "https:" + trimmedUrl;
        if (!Uri.TryCreate(trimmedUrl, UriKind.Absolute, out var parsedUri))
            return null;
        if (parsedUri.Scheme != Uri.UriSchemeHttp && parsedUri.Scheme != Uri.UriSchemeHttps)
            return null;
        return trimmedUrl;
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
                artist = _db.Artists.Local.FirstOrDefault(x => x.DiscogsArtistId == apiId);
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
                label = _db.Labels.Local.FirstOrDefault(x => x.DiscogsLabelId == apiId);
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
                var personalAccessToken = await TryGetDiscogsPersonalAccessTokenForUserAsync(applicationUserId, cancellationToken);
                if (personalAccessToken is null)
                {
                    _logger.LogInformation(
                        "Skipping Discogs collection value refresh: no token for user {UserId}",
                        applicationUserId);
                    return;
                }

                _discogsAuthenticationService.AuthenticateWithPersonalAccessToken(personalAccessToken);
                var collectionValueFromDiscogs = await _discogsApiClient.GetCollectionValue(discogsUsername, cancellationToken);
                var userRow = await _db.Users.FirstOrDefaultAsync(x => x.Id == applicationUserId, cancellationToken);
                if (userRow is null)
                    return;

                userRow.DiscogsCollectionValueMin     = collectionValueFromDiscogs.Minimum;
                userRow.DiscogsCollectionValueMedian  = collectionValueFromDiscogs.Median;
                userRow.DiscogsCollectionValueMax     = collectionValueFromDiscogs.Maximum;
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

    public async Task SyncCollectionInBackground(CancellationToken cancellationToken)
    {
        var usersWithDiscogs = await _db.Users
            .Where(x => x.DiscogsUsername != null)
            .ToListAsync(cancellationToken);

        _logger.LogInformation(
            "Starting scheduled Discogs collection sync for {UserCount} users.",
            usersWithDiscogs.Count);

        var artistCache = await ArtistSyncCache.LoadAsync(_db, cancellationToken);
        var labelCache  = await LabelSyncCache.LoadAsync(_db, cancellationToken);

        foreach (var user in usersWithDiscogs)
        {
            if (string.IsNullOrWhiteSpace(user.DiscogsUsername))
                continue;

            await SyncDiscogsUserCollectionFoldersAsync(user.DiscogsUsername!, user.Id, artistCache, labelCache, cancellationToken);
            await TryRefreshStoredCollectionValueAsync(user.DiscogsUsername!, user.Id, cancellationToken);

            await Task.Delay(DiscogsRequestDelay, cancellationToken);
        }

        _logger.LogInformation(
            "Scheduled Discogs collection sync complete for {UserCount} users.",
            usersWithDiscogs.Count);
    }

    public async Task SyncUserInBackground(string applicationUserId, CancellationToken cancellationToken)
    {
        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == applicationUserId, cancellationToken);
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

        var artistCache = await ArtistSyncCache.LoadAsync(_db, cancellationToken);
        var labelCache  = await LabelSyncCache.LoadAsync(_db, cancellationToken);

        await SyncDiscogsUserCollectionFoldersAsync(user.DiscogsUsername!, user.Id, artistCache, labelCache, cancellationToken);
        await TryRefreshStoredCollectionValueAsync(user.DiscogsUsername!, user.Id, cancellationToken);

        _logger.LogInformation("Discogs collection sync complete for {Username} ({UserId})", user.DiscogsUsername, user.Id);

        await Task.Delay(DiscogsRequestDelay, cancellationToken);
    }

    public async Task SyncUserFullInBackground(string applicationUserId, CancellationToken cancellationToken)
    {
        await SyncUserInBackground(applicationUserId, cancellationToken);
        await SyncReleaseDetails(cancellationToken, applicationUserId);
        await DownloadMissingImages(cancellationToken, applicationUserId);
        await RefreshAllArtistLabelDiscogsDetailsAsync(cancellationToken, applicationUserId);
    }

    public async Task DownloadMissingImages(CancellationToken cancellationToken, string? restrictToApplicationUserId = null)
    {
        await _discogsExclusiveGate.WaitAsync(cancellationToken);
        try
        {
            await DownloadMissingImagesCore(cancellationToken, restrictToApplicationUserId);
        }
        finally
        {
            _discogsExclusiveGate.Release();
        }
    }

    private async Task DownloadMissingImagesCore(CancellationToken cancellationToken, string? restrictToApplicationUserId = null)
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
            from releaseToUserAssociation in releaseCover.Release.UserAssociations
            join collectionOwner in _db.Users on releaseToUserAssociation.UserId equals collectionOwner.Id
            where collectionOwner.DiscogsUsername != null
                  && !string.IsNullOrWhiteSpace(collectionOwner.DiscogsUsername)
                  && (restrictToApplicationUserId == null || releaseToUserAssociation.UserId == restrictToApplicationUserId)
            select new
            {
                Cover = releaseCover,
                OwnerDiscogsUsername = collectionOwner.DiscogsUsername!.Trim(),
                AlbumName = releaseCover.Release.Album,
            }).ToListAsync(cancellationToken);

        var distinctDownloadTargets = coverRowsForOwnersWithDiscogsUsername
            .GroupBy(x => new { x.Cover.Id, x.OwnerDiscogsUsername })
            .Select(x => x.First())
            .ToList();

        if (restrictToApplicationUserId is null)
        {
            _logger.LogInformation(
                "Checking {Count} release cover download targets (per Discogs collection owner).",
                distinctDownloadTargets.Count);
        }
        else
        {
            _logger.LogInformation(
                "Checking {Count} release cover download targets for user {UserId}.",
                distinctDownloadTargets.Count,
                restrictToApplicationUserId);
        }

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

                    fullSizeBytes = await _http.GetByteArrayAsync(coverUri, cancellationToken);
                    var filename = $"{releaseCover.DiscogsReleaseId}.jpg";
                    var path = Path.Combine(perUserCoverDirectory, filename);
                    await File.WriteAllBytesAsync(path, fullSizeBytes, cancellationToken);

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
                        perUserCoverDirectory, releaseCover.LocalImageFilename!, cancellationToken);
                    var thumbFilename = $"{releaseCover.DiscogsReleaseId}-thumb.jpg";
                    var thumbPath = Path.Combine(perUserCoverDirectory, thumbFilename);
                    await WriteThumbnailAsync(fullSizeBytes, thumbPath, cancellationToken);
                    releaseCover.LocalThumbnailFilename = thumbFilename;
                }

                await _db.SaveChangesAsync(cancellationToken);

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
                await Task.Delay(DiscogsRequestDelay, cancellationToken);
            }
        }

        await DownloadMissingArtistImagesCore(baseImageDir, cancellationToken, restrictToApplicationUserId);
        await DownloadMissingLabelImagesCore(baseImageDir, cancellationToken, restrictToApplicationUserId);

        if (restrictToApplicationUserId is null)
        {
            _logger.LogInformation(
                "Release cover asset checks complete for all Discogs owners. Prepared {PreparedCount}; failed {FailedCount}.",
                preparedCount,
                failedCount);
        }
        else
        {
            _logger.LogInformation(
                "Release cover asset checks complete for user {UserId}. Prepared {PreparedCount}; failed {FailedCount}.",
                restrictToApplicationUserId,
                preparedCount,
                failedCount);
        }
    }

    private async Task DownloadMissingArtistImagesCore(
        string catalogImagesRootDirectory,
        CancellationToken cancellationToken,
        string? restrictToApplicationUserId = null)
    {
        var artistProfilesDirectory =
            Path.Combine(catalogImagesRootDirectory, CoverStoragePathResolver.SharedArtistProfileSubfolder);
        Directory.CreateDirectory(artistProfilesDirectory);

        var imageCandidates = await _db.Artists
            .Where(x => (x.DiscogsImageUrl != null || x.LocalImageFilename != null || x.LocalThumbnailFilename != null)
                        && (restrictToApplicationUserId == null
                            || x.Releases.Any(r => r.UserAssociations.Any(u => u.UserId == restrictToApplicationUserId))))
            .Select(x => new
            {
                Artist = x,
                x.Name,
            })
            .ToListAsync(cancellationToken);

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

                    fullSizeBytes = await _http.GetByteArrayAsync(imageUri, cancellationToken);
                    var filename = $"artist-{imageKey}.jpg";
                    var path = Path.Combine(artistProfilesDirectory, filename);
                    await File.WriteAllBytesAsync(path, fullSizeBytes, cancellationToken);

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
                        cancellationToken);
                    var thumbFilename = $"artist-{imageKey}-thumb.jpg";
                    var thumbPath = Path.Combine(artistProfilesDirectory, thumbFilename);
                    await WriteThumbnailAsync(fullSizeBytes, thumbPath, cancellationToken);
                    artist.LocalThumbnailFilename = thumbFilename;
                }

                await _db.SaveChangesAsync(cancellationToken);
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
                await Task.Delay(DiscogsRequestDelay, cancellationToken);
            }
        }

        _logger.LogInformation(
            "Artist image asset check complete. Prepared {PreparedCount}; failed {FailedCount}.",
            preparedCount,
            failedCount);
    }

    private async Task DownloadMissingLabelImagesCore(
        string catalogImagesRootDirectory,
        CancellationToken cancellationToken,
        string? restrictToApplicationUserId = null)
    {
        var labelProfilesDirectory =
            Path.Combine(catalogImagesRootDirectory, CoverStoragePathResolver.SharedLabelProfileSubfolder);
        Directory.CreateDirectory(labelProfilesDirectory);

        var imageCandidates = await _db.Labels
            .Where(x => (x.DiscogsImageUrl != null || x.LocalImageFilename != null || x.LocalThumbnailFilename != null)
                        && (restrictToApplicationUserId == null
                            || x.Releases.Any(r => r.UserAssociations.Any(u => u.UserId == restrictToApplicationUserId))))
            .Select(x => new
            {
                Label = x,
                x.Name,
            })
            .ToListAsync(cancellationToken);

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

                    fullSizeBytes = await _http.GetByteArrayAsync(imageUri, cancellationToken);
                    var filename = $"label-{imageKey}.jpg";
                    var path = Path.Combine(labelProfilesDirectory, filename);
                    await File.WriteAllBytesAsync(path, fullSizeBytes, cancellationToken);

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
                        cancellationToken);
                    var thumbFilename = $"label-{imageKey}-thumb.jpg";
                    var thumbPath = Path.Combine(labelProfilesDirectory, thumbFilename);
                    await WriteThumbnailAsync(fullSizeBytes, thumbPath, cancellationToken);
                    label.LocalThumbnailFilename = thumbFilename;
                }

                await _db.SaveChangesAsync(cancellationToken);
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
                await Task.Delay(DiscogsRequestDelay, cancellationToken);
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
        CancellationToken cancellationToken)
    {
        var pathUnderDiscogsUser = Path.Combine(perDiscogsUserCoverDirectory, storedFileNameOnly);
        if (!File.Exists(pathUnderDiscogsUser))
            throw new FileNotFoundException("Release cover file not found in per-discogs-user folder.", pathUnderDiscogsUser);

        return await File.ReadAllBytesAsync(pathUnderDiscogsUser, cancellationToken);
    }

    private static async Task<byte[]> ReadExistingCatalogImageBytesAsync(
        string catalogImagesRootDirectory,
        string storedFileNameOnly,
        CancellationToken cancellationToken)
    {
        var fullImagePath = Path.Combine(catalogImagesRootDirectory, storedFileNameOnly);
        return await File.ReadAllBytesAsync(fullImagePath, cancellationToken);
    }

    private static async Task WriteThumbnailAsync(byte[] sourceBytes, string outputPath, CancellationToken cancellationToken)
    {
        await using var sourceStream = new MemoryStream(sourceBytes);
        using var image = await Image.LoadAsync(sourceStream, cancellationToken);
        image.Mutate(x => x.Resize(new ResizeOptions
        {
            Mode = ResizeMode.Max,
            Size = new Size(ThumbnailMaxWidth, ThumbnailMaxWidth),
        }));

        var encoder = new JpegEncoder { Quality = ThumbnailQuality };
        await image.SaveAsJpegAsync(outputPath, encoder, cancellationToken);
    }

    public async Task SyncReleaseDetails(CancellationToken cancellationToken, string? restrictToApplicationUserId = null)
    {
        await _discogsExclusiveGate.WaitAsync(cancellationToken);
        try
        {
            await SyncReleaseDetailsCore(cancellationToken, restrictToApplicationUserId);
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
        CancellationToken cancellationToken)
    {
        await _db.ReleaseGenres
            .Where(x => x.ReleaseId == releaseId)
            .ExecuteDeleteAsync(cancellationToken);
        await _db.ReleaseStyles
            .Where(x => x.ReleaseId == releaseId)
            .ExecuteDeleteAsync(cancellationToken);

        if (genres is not null)
        {
            var requestedGenres = genres
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Select(y => new { Trimmed = y, Normalized = y.ToLowerInvariant() })
                .DistinctBy(x => x.Normalized)
                .ToList();

            var requestedGenreKeys = requestedGenres.Select(x => x.Normalized).ToList();
            var existingGenres = await _db.Genres
                .Where(x => requestedGenreKeys.Contains(x.NormalizedName))
                .ToDictionaryAsync(x => x.NormalizedName, y => y, StringComparer.Ordinal, cancellationToken);

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
            .Select(y => new { Trimmed = y, Normalized = y.ToLowerInvariant() })
            .DistinctBy(x => x.Normalized)
            .ToList();

        var requestedStyleKeys = requestedStyles.Select(x => x.Normalized).ToList();
        var existingStyles = await _db.Styles
            .Where(x => requestedStyleKeys.Contains(x.NormalizedName))
            .ToDictionaryAsync(x => x.NormalizedName, y => y, StringComparer.Ordinal, cancellationToken);

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

    private readonly record struct ReleaseDetailSyncCandidate(int Id, int DiscogsReleaseId, string Album);

    private async Task SyncReleaseDetailsCore(CancellationToken cancellationToken, string? restrictToApplicationUserId = null)
    {
        var releaseQuery = _db.Releases
            .AsNoTracking()
            .Where(x => x.SchemaVersion < Release.ReleaseSchemaVersion);

        if (restrictToApplicationUserId is not null)
            releaseQuery = releaseQuery.Where(x =>
                x.UserAssociations.Any(u => u.UserId == restrictToApplicationUserId));

        var releaseDetailSyncCandidates = await releaseQuery
            .Select(x => new ReleaseDetailSyncCandidate(x.Id, x.DiscogsReleaseId, x.Album))
            .ToListAsync(cancellationToken);

        if (restrictToApplicationUserId is null)
        {
            _logger.LogInformation(
                "Syncing Discogs details for {ReleaseCount} releases behind schema version {ExpectedVersion}.",
                releaseDetailSyncCandidates.Count,
                Release.ReleaseSchemaVersion);
        }
        else
        {
            _logger.LogInformation(
                "Syncing Discogs details for {ReleaseCount} releases (user {UserId}) behind schema version {ExpectedVersion}.",
                releaseDetailSyncCandidates.Count,
                restrictToApplicationUserId,
                Release.ReleaseSchemaVersion);
        }

        var syncedCount = 0;
        var failedCount = 0;

        foreach (var releaseCandidate in releaseDetailSyncCandidates)
        {
            try
            {
                var discogsRelease = await _discogsApiClient.GetRelease(releaseCandidate.DiscogsReleaseId, cancellationToken);

                var formatSummaryForDb = DiscogsReleaseFormatFormatter.BuildFormatSummary(discogsRelease.Formats) ?? "";
                var notesTextForDb = string.IsNullOrWhiteSpace(discogsRelease.Notes)
                    ? ""
                    : discogsRelease.Notes.Trim();
                var communityHaveCount = discogsRelease.CommunityStatistics?.UsersOwningReleaseCount;
                var communityWantCount = discogsRelease.CommunityStatistics?.UsersWantingReleaseCount;

                var tracklistAlreadyImported =
                    await _db.Tracks.AnyAsync(x => x.ReleaseId == releaseCandidate.Id, cancellationToken);

                if (tracklistAlreadyImported)
                {
                    await _db.Releases.Where(x => x.Id == releaseCandidate.Id)
                        .ExecuteUpdateAsync(
                            s => s
                                .SetProperty(x => x.CommunityHaveCount, communityHaveCount)
                                .SetProperty(x => x.CommunityWantCount, communityWantCount)
                                .SetProperty(x => x.Format, formatSummaryForDb)
                                .SetProperty(x => x.Notes, notesTextForDb)
                                .SetProperty(x => x.SchemaVersion, Release.ReleaseSchemaVersion),
                            cancellationToken);

                    _logger.LogInformation(
                        "Synced {Album} ({DiscogsReleaseId})",
                        releaseCandidate.Album,
                        releaseCandidate.DiscogsReleaseId);
                    syncedCount++;
                }
                else
                {
                    await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

                    if (await _db.Tracks.AnyAsync(x => x.ReleaseId == releaseCandidate.Id, cancellationToken))
                    {
                        _logger.LogInformation(
                            "Skipping track import for {Album} ({DiscogsReleaseId}); tracks appeared during fetch.",
                            releaseCandidate.Album,
                            releaseCandidate.DiscogsReleaseId);
                        await _db.Releases.Where(x => x.Id == releaseCandidate.Id)
                            .ExecuteUpdateAsync(
                                s => s
                                    .SetProperty(x => x.CommunityHaveCount, communityHaveCount)
                                    .SetProperty(x => x.CommunityWantCount, communityWantCount)
                                    .SetProperty(x => x.Format, formatSummaryForDb)
                                    .SetProperty(x => x.Notes, notesTextForDb)
                                    .SetProperty(x => x.SchemaVersion, Release.ReleaseSchemaVersion),
                                cancellationToken);
                        await transaction.CommitAsync(cancellationToken);
                        syncedCount++;
                        continue;
                    }

                    await ReplaceReleaseGenresAndStylesAsync(
                        releaseCandidate.Id,
                        discogsRelease.Genres,
                        discogsRelease.Styles,
                        cancellationToken);

                    await _db.Releases.Where(x => x.Id == releaseCandidate.Id)
                        .ExecuteUpdateAsync(
                            s => s
                                .SetProperty(x => x.CommunityHaveCount, communityHaveCount)
                                .SetProperty(x => x.CommunityWantCount, communityWantCount)
                                .SetProperty(x => x.Format, formatSummaryForDb)
                                .SetProperty(x => x.Notes, notesTextForDb)
                                .SetProperty(x => x.SchemaVersion, Release.ReleaseSchemaVersion),
                            cancellationToken);

                    foreach (var trackEntry in discogsRelease.Tracklist.Where(listItem => listItem.Type == "track"))
                    {
                        var durationSeconds = TrackDurationParser.TryParseSeconds(trackEntry.Duration);
                        _db.Tracks.Add(new Track
                        {
                            ReleaseId = releaseCandidate.Id,
                            Position = trackEntry.Position,
                            Title = trackEntry.Title,
                            Duration = trackEntry.Duration,
                            DurationSeconds = durationSeconds is > 0 ? durationSeconds : null,
                        });
                    }

                    await _db.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);

                    _logger.LogInformation(
                        "Synced details for {Album} ({DiscogsReleaseId})",
                        releaseCandidate.Album,
                        releaseCandidate.DiscogsReleaseId);
                    syncedCount++;
                }
            }
            catch (RateLimitExceededDiscogsException)
            {
                _logger.LogWarning(
                    "Discogs rate limited while syncing {Album} ({DiscogsReleaseId}); backing off.",
                    releaseCandidate.Album,
                    releaseCandidate.DiscogsReleaseId);
                failedCount++;
                await Task.Delay(RateLimitBackoff, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to sync details for {Album} ({DiscogsReleaseId})",
                    releaseCandidate.Album,
                    releaseCandidate.DiscogsReleaseId);
                _db.ChangeTracker.Clear();
                failedCount++;
            }
            finally
            {
                await Task.Delay(ReleaseDetailsRequestDelay, cancellationToken);
            }
        }

        _logger.LogInformation(
            "Discogs release detail sync complete. Synced {SyncedCount}; failed {FailedCount}.",
            syncedCount,
            failedCount);
    }

    public async Task RefreshAllArtistLabelDiscogsDetailsAsync(
        CancellationToken cancellationToken,
        string? restrictToApplicationUserId = null)
    {
        var artistsQuery = _db.Artists.AsNoTracking()
            .Where(x => x.DiscogsArtistId != null && x.SchemaVersion < Artist.ArtistSchemaVersion);

        if (restrictToApplicationUserId is not null)
        {
            artistsQuery = artistsQuery.Where(a =>
                a.Releases.Any(r => r.UserAssociations.Any(u => u.UserId == restrictToApplicationUserId)));
        }

        var artists = await artistsQuery
            .Select(x => new ArtistMetadataRefreshRow(x.Id, x.DiscogsArtistId!.Value))
            .ToListAsync(cancellationToken);

        if (restrictToApplicationUserId is null)
        {
            _logger.LogInformation(
                "Refreshing Discogs metadata for {ArtistCount} artists.",
                artists.Count);
        }
        else
        {
            _logger.LogInformation(
                "Refreshing Discogs metadata for {ArtistCount} artists (user {UserId}).",
                artists.Count,
                restrictToApplicationUserId);
        }

        foreach (var artist in artists)
            await RefreshArtistDiscogsDetailsAsync(artist, cancellationToken);

        var labelsQuery = _db.Labels.AsNoTracking()
            .Where(x => x.DiscogsLabelId != null && x.SchemaVersion < Label.LabelSchemaVersion);

        if (restrictToApplicationUserId is not null)
        {
            labelsQuery = labelsQuery.Where(l =>
                l.Releases.Any(r => r.UserAssociations.Any(u => u.UserId == restrictToApplicationUserId)));
        }

        var labels = await labelsQuery
            .Select(x => new LabelMetadataRefreshRow(x.Id, x.DiscogsLabelId!.Value))
            .ToListAsync(cancellationToken);

        if (restrictToApplicationUserId is null)
        {
            _logger.LogInformation(
                "Refreshing Discogs metadata for {LabelCount} labels.",
                labels.Count);
        }
        else
        {
            _logger.LogInformation(
                "Refreshing Discogs metadata for {LabelCount} labels (user {UserId}).",
                labels.Count,
                restrictToApplicationUserId);
        }

        foreach (var label in labels)
            await RefreshLabelDiscogsDetailsAsync(label, cancellationToken);

        _logger.LogInformation(
            "Discogs artist/label metadata refresh complete for {ArtistCount} artists and {LabelCount} labels.",
            artists.Count,
            labels.Count);
    }

    private readonly record struct ArtistMetadataRefreshRow(int Id, int DiscogsArtistId);

    private readonly record struct LabelMetadataRefreshRow(int Id, int DiscogsLabelId);

    private async Task RefreshArtistDiscogsDetailsAsync(ArtistMetadataRefreshRow row, CancellationToken cancellationToken)
    {
        try
        {
            _memoryCache.Remove(DiscogsMemoryCacheKeys.ArtistDetails(row.DiscogsArtistId));

            await _discogsExclusiveGate.WaitAsync(cancellationToken);
            try
            {
                var artist = await _db.Artists.FirstAsync(x => x.Id == row.Id, cancellationToken);
                var artistDetailsFromDiscogs = await _discogsApiClient.GetArtist(row.DiscogsArtistId, cancellationToken);
                artist.DiscogsProfile = artistDetailsFromDiscogs.Profile;

                if (string.IsNullOrWhiteSpace(artist.DiscogsImageUrl))
                    artist.DiscogsImageUrl = DiscogsApiImages.PrimaryOrFirstUri(artistDetailsFromDiscogs.Images);

                artist.SchemaVersion = Artist.ArtistSchemaVersion;
                artist.DiscogsDetailsFetchedAt = DateTimeOffset.UtcNow;
                await _db.SaveChangesAsync(cancellationToken);
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
            await Task.Delay(RateLimitBackoff, cancellationToken);
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
            await Task.Delay(DiscogsRequestDelay, cancellationToken);
        }
    }

    private async Task RefreshLabelDiscogsDetailsAsync(LabelMetadataRefreshRow row, CancellationToken cancellationToken)
    {
        try
        {
            _memoryCache.Remove(DiscogsMemoryCacheKeys.LabelDetails(row.DiscogsLabelId));

            await _discogsExclusiveGate.WaitAsync(cancellationToken);
            try
            {
                var label = await _db.Labels.FirstAsync(x => x.Id == row.Id, cancellationToken);
                var labelDetailsFromDiscogs = await _discogsApiClient.GetLabel(row.DiscogsLabelId, cancellationToken);
                label.DiscogsProfile = labelDetailsFromDiscogs.Profile;

                if (string.IsNullOrWhiteSpace(label.DiscogsImageUrl))
                    label.DiscogsImageUrl = DiscogsApiImages.PrimaryOrFirstUri(labelDetailsFromDiscogs.Images);

                label.SchemaVersion = Label.LabelSchemaVersion;
                label.DiscogsDetailsFetchedAt = DateTimeOffset.UtcNow;
                await _db.SaveChangesAsync(cancellationToken);
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
            await Task.Delay(RateLimitBackoff, cancellationToken);
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
            await Task.Delay(DiscogsRequestDelay, cancellationToken);
        }
    }

    private sealed class ArtistSyncCache
    {
        private readonly Dictionary<string, Artist> _byName;
        private readonly Dictionary<int, Artist> _byDiscogsId;

        public static async Task<ArtistSyncCache> LoadAsync(ApplicationDbContext db, CancellationToken cancellationToken)
        {
            var allArtists = await db.Artists.AsNoTracking().ToListAsync(cancellationToken);
            var byName = new Dictionary<string, Artist>(StringComparer.Ordinal);
            foreach (var artist in allArtists)
            {
                if (!byName.ContainsKey(artist.Name))
                    byName[artist.Name] = artist;
            }

            var byDiscogsId = new Dictionary<int, Artist>();
            foreach (var artist in allArtists)
            {
                if (artist.DiscogsArtistId is not int discogsArtistIdForRow || discogsArtistIdForRow <= 0)
                    continue;
                if (!byDiscogsId.ContainsKey(discogsArtistIdForRow))
                    byDiscogsId[discogsArtistIdForRow] = artist;
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
            if (artist.DiscogsArtistId is int mappedDiscogsArtistId && mappedDiscogsArtistId > 0
                && !_byDiscogsId.ContainsKey(mappedDiscogsArtistId))
                _byDiscogsId[mappedDiscogsArtistId] = artist;
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

        public static async Task<LabelSyncCache> LoadAsync(ApplicationDbContext db, CancellationToken cancellationToken)
        {
            var allLabels = await db.Labels.AsNoTracking().ToListAsync(cancellationToken);
            var byName = new Dictionary<string, Label>(StringComparer.Ordinal);
            foreach (var labelRow in allLabels)
            {
                if (!byName.ContainsKey(labelRow.Name))
                    byName[labelRow.Name] = labelRow;
            }

            var byDiscogsId = new Dictionary<int, Label>();
            foreach (var labelRow in allLabels)
            {
                if (labelRow.DiscogsLabelId is not int discogsLabelIdForRow || discogsLabelIdForRow <= 0)
                    continue;
                if (!byDiscogsId.ContainsKey(discogsLabelIdForRow))
                    byDiscogsId[discogsLabelIdForRow] = labelRow;
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
            if (label.DiscogsLabelId is int mappedDiscogsLabelId && mappedDiscogsLabelId > 0
                && !_byDiscogsId.ContainsKey(mappedDiscogsLabelId))
                _byDiscogsId[mappedDiscogsLabelId] = label;
        }

        public void RememberDiscogsIdMapping(Label label, int discogsLabelId)
        {
            if (discogsLabelId <= 0 || _byDiscogsId.ContainsKey(discogsLabelId))
                return;
            _byDiscogsId[discogsLabelId] = label;
        }
    }
}
