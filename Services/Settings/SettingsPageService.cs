using DiscogScrobblerMVC.Data;
using DiscogScrobblerMVC.Data.Entities;
using DiscogScrobblerMVC.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;

namespace DiscogScrobblerMVC.Services;

public class SettingsPageService : ISettingsPageService
{
    private const int MaxLastFmCoverCandidates = 120;

    private static readonly DistributedCacheEntryOptions PendingLastFmTokenCacheOptions =
        new() { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(45) };

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _db;
    private readonly IDiscogsSyncQueue _syncQueue;
    private readonly IDiscogsMetadataRefreshQueue _metadataRefreshQueue;
    private readonly ILastFmOAuthService _lastFmOAuth;
    private readonly IDistributedCache _cache;
    private readonly ILogger<SettingsPageService> _logger;

    public SettingsPageService(
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext db,
        IDiscogsSyncQueue syncQueue,
        IDiscogsMetadataRefreshQueue metadataRefreshQueue,
        ILastFmOAuthService lastFmOAuth,
        IDistributedCache cache,
        ILogger<SettingsPageService> logger)
    {
        _userManager = userManager;
        _db = db;
        _syncQueue = syncQueue;
        _metadataRefreshQueue = metadataRefreshQueue;
        _lastFmOAuth = lastFmOAuth;
        _cache = cache;
        _logger = logger;
    }

    public async Task<SettingsViewModel> BuildViewModelAsync(
        ApplicationUser user,
        string lastFmCallbackUri,
        CancellationToken cancellationToken,
        SettingsViewModel? existingViewModel = null)
    {
        var viewModel = existingViewModel ?? new SettingsViewModel { DiscogsUsername = user.DiscogsUsername };

        viewModel.LastFmConfiguredOnServer = _lastFmOAuth.IsConfigured;
        viewModel.LastFmConnected = !string.IsNullOrWhiteSpace(user.LastFmSessionKey);
        viewModel.LastFmUsername = user.LastFmUsername;
        viewModel.LastFmSuggestedCallbackUri = lastFmCallbackUri;

        viewModel.HasPendingLastFmToken =
            !string.IsNullOrEmpty(await _cache.GetStringAsync(PendingLastFmCacheKey(user.Id), cancellationToken));

        var coverCandidates = await _db.Releases
            .AsNoTracking()
            .Where(x => x.UserAssociations.Any(x => x.UserId == user.Id))
            .Select(x => x.Images == null
                ? null
                : new
                {
                    x.Images.LocalThumbnailFilename,
                    x.Images.LocalImageFilename,
                    x.Images.CoverUrl
                })
            .ToListAsync(cancellationToken);

        viewModel.LastFmCoverCandidates = coverCandidates
            .Select(x => x == null
                ? null
                : CoverImageUrlResolver.ResolveForGrid(
                    x.LocalThumbnailFilename,
                    x.LocalImageFilename,
                    x.CoverUrl))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)
            .Distinct()
            .Take(MaxLastFmCoverCandidates)
            .ToList();

        return viewModel;
    }

    public async Task<SettingsSaveResult> SaveDiscogsUsernameAsync(
        ApplicationUser user,
        string? discogsUsername,
        CancellationToken cancellationToken)
    {
        user.DiscogsUsername = string.IsNullOrWhiteSpace(discogsUsername) ? null : discogsUsername.Trim();

        var result = await _userManager.UpdateAsync(user);
        if (result.Succeeded)
            return new SettingsSaveResult(true, "Settings saved.", []);

        var errors = result.Errors.Select(x => x.Description).ToList();
        _logger.LogError(
            "Saving Discogs settings failed for user {UserId}: {Errors}",
            user.Id,
            string.Join("; ", result.Errors.Select(x => $"{x.Code}:{x.Description}")));

        return new SettingsSaveResult(false, string.Empty, errors);
    }

    public async Task<LastFmConnectResult> StartLastFmConnectionAsync(
        ApplicationUser user,
        string lastFmCallbackUri,
        CancellationToken cancellationToken)
    {
        var (ready, authUrl, error) = await _lastFmOAuth.GetAuthorizationUrlAsync(lastFmCallbackUri, cancellationToken);
        if (!ready || string.IsNullOrWhiteSpace(authUrl))
            return new LastFmConnectResult(false, null, $"Last.fm connection couldn’t start. {error}");

        if (!_lastFmOAuth.TryExtractTokenFromAuthorizeUrl(authUrl, out var token))
            return new LastFmConnectResult(false, null, "Last.fm returned an invalid authorization URL.");

        await _cache.SetStringAsync(
            PendingLastFmCacheKey(user.Id),
            token!,
            PendingLastFmTokenCacheOptions,
            cancellationToken);

        return new LastFmConnectResult(true, authUrl, string.Empty);
    }

    public async Task<string> CompleteLastFmCallbackAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        var cacheKey = PendingLastFmCacheKey(user.Id);
        var pending = await _cache.GetStringAsync(cacheKey, cancellationToken);
        await _cache.RemoveAsync(cacheKey, cancellationToken);

        if (string.IsNullOrWhiteSpace(pending))
        {
            _logger.LogWarning("Last.fm callback with no cached pending token for user {UserId}", user.Id);
            return "Last.fm authorization expired or ran out of sync. Go to Settings → Connect Last.fm once more.";
        }

        var (ok, sessionKey, lastFmUsername, exchangeError) =
            await _lastFmOAuth.ExchangeTokenAsync(pending, cancellationToken);

        if (!ok || string.IsNullOrWhiteSpace(sessionKey))
            return $"Could not verify Last.fm: {exchangeError}";

        user.LastFmSessionKey = sessionKey;
        user.LastFmUsername = lastFmUsername;

        var saved = await _userManager.UpdateAsync(user);
        if (!saved.Succeeded)
        {
            var details = string.Join("; ", saved.Errors.Select(x => $"{x.Code}: {x.Description}"));
            _logger.LogError("Persisting Last.fm session key failed after OAuth for user {UserId}: {Details}", user.Id, details);

            user.LastFmSessionKey = null;
            user.LastFmUsername = null;
            return $"Last.fm replied OK but this app could not save your session: {details}";
        }

        _logger.LogInformation("Last.fm OAuth stored for Identity user {UserId} ({LastFmUsername})", user.Id, lastFmUsername ?? "?");

        return lastFmUsername != null
            ? $"Last.fm connected ({lastFmUsername}). You can start scrobbling."
            : "Last.fm connected. You can start scrobbling.";
    }

    public async Task<string> DisconnectLastFmAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        user.LastFmSessionKey = null;
        user.LastFmUsername = null;

        await _cache.RemoveAsync(PendingLastFmCacheKey(user.Id), cancellationToken);

        var saved = await _userManager.UpdateAsync(user);
        if (saved.Succeeded)
            return "Last.fm disconnected from this profile.";

        var details = string.Join("; ", saved.Errors.Select(x => $"{x.Code}: {x.Description}"));
        _logger.LogError("Disconnect Last.fm failed for user {UserId}: {Details}", user.Id, details);

        return $"Could not disconnect Last.fm: {details}";
    }

    public string StartDiscogsSync(ApplicationUser user)
    {
        if (string.IsNullOrWhiteSpace(user.DiscogsUsername))
            return "Please set your Discogs username first.";

        var enqueued = _syncQueue.EnqueueUserFullSync(user.Id);
        return enqueued
            ? "Sync started in the background."
            : "Could not start sync — try again.";
    }

    public string RefreshDiscogsArtistLabelDetails()
    {
        var enqueued = _metadataRefreshQueue.EnqueueRefreshAllArtistLabelDetails();
        return enqueued
            ? "Artist and label metadata refresh started in the background."
            : "Could not start metadata refresh — try again.";
    }

    private static string PendingLastFmCacheKey(string userId) => $"lastfm:pending-token:{userId}";
}
