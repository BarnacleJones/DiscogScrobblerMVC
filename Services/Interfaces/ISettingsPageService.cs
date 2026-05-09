using DiscogScrobblerMVC.Data.Entities;
using DiscogScrobblerMVC.Models;

namespace DiscogScrobblerMVC.Services.Interfaces;

public interface ISettingsPageService
{
    Task<SettingsViewModel> BuildViewModelAsync(
        ApplicationUser user,
        string lastFmCallbackUri,
        CancellationToken cancellationToken,
        SettingsViewModel? existingViewModel = null);

    Task<SettingsSaveResult> SaveDiscogsSettingsAsync(
        ApplicationUser user,
        string? discogsUsername,
        string? personalAccessTokenSubmission,
        bool clearPersonalAccessToken,
        CancellationToken cancellationToken);

    Task<LastFmConnectResult> StartLastFmConnectionAsync(
        ApplicationUser user,
        string lastFmCallbackUri,
        CancellationToken cancellationToken);

    Task<string> CompleteLastFmCallbackAsync(ApplicationUser user, CancellationToken cancellationToken);

    Task<string> DisconnectLastFmAsync(ApplicationUser user, CancellationToken cancellationToken);

    string StartDiscogsSync(ApplicationUser user);
}

public record SettingsSaveResult(bool Succeeded, string StatusMessage, IReadOnlyList<string> Errors);

public record LastFmConnectResult(bool Started, string? AuthUrl, string StatusMessage);
