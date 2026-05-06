using DiscogScrobblerMVC.Data.Entities;
using DiscogScrobblerMVC.Models;
using DiscogScrobblerMVC.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace DiscogScrobblerMVC.Controllers;

[Authorize]
public class SettingsController : ApplicationController
{
    private const string HostingPublicBaseUrlKey = "Hosting:PublicBaseUrl";

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ISettingsPageService _settingsPageService;
    private readonly IConfiguration _configuration;

    public SettingsController(
        UserManager<ApplicationUser> userManager,
        ISettingsPageService settingsPageService,
        IConfiguration configuration)
    {
        _userManager = userManager;
        _settingsPageService = settingsPageService;
        _configuration = configuration;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
            return Challenge();

        var viewModel = await _settingsPageService.BuildViewModelAsync(
            user,
            BuildLastFmAuthorizationCallbackUri(),
            cancellationToken);

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(SettingsViewModel submission, CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
            return Challenge();

        if (!ModelState.IsValid)
            return View(await BuildViewModelWithSubmissionAsync(user, submission, cancellationToken));

        var saveResult = await _settingsPageService.SaveDiscogsUsernameAsync(
            user,
            submission.DiscogsUsername,
            cancellationToken);

        if (!saveResult.Succeeded)
        {
            foreach (var error in saveResult.Errors)
                ModelState.AddModelError(string.Empty, error);

            return View(await BuildViewModelWithSubmissionAsync(user, submission, cancellationToken));
        }

        TempData["StatusMessage"] = saveResult.StatusMessage;
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Begins Last.fm web auth - stores the interim token server-side then redirects to Last.fm.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ConnectLastFm(CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
            return Challenge();

        var connectionResult = await _settingsPageService.StartLastFmConnectionAsync(
            user,
            BuildLastFmAuthorizationCallbackUri(),
            cancellationToken);

        if (connectionResult.Started && !string.IsNullOrWhiteSpace(connectionResult.AuthUrl))
            return Redirect(connectionResult.AuthUrl);

        TempData["StatusMessage"] = connectionResult.StatusMessage;
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Completes Last.fm web auth after Last.fm redirects the browser back, or after Finish if redirect failed.</summary>
    [HttpGet]
    public async Task<IActionResult> LastFmCallback(CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
            return Challenge();

        TempData["StatusMessage"] = await _settingsPageService.CompleteLastFmCallbackAsync(user, cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DisconnectLastFm(CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
            return Challenge();

        TempData["StatusMessage"] = await _settingsPageService.DisconnectLastFmAsync(user, cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SyncDiscogs()
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
            return Challenge();

        TempData["StatusMessage"] = _settingsPageService.StartDiscogsSync(user);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RefreshDiscogsArtistLabelDetails()
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
            return Challenge();

        TempData["StatusMessage"] = _settingsPageService.RefreshDiscogsArtistLabelDetails();
        return RedirectToAction(nameof(Index));
    }

    private async Task<ApplicationUser?> GetCurrentUserAsync() =>
        await _userManager.GetUserAsync(User);

    private async Task<SettingsViewModel> BuildViewModelWithSubmissionAsync(
        ApplicationUser user,
        SettingsViewModel submission,
        CancellationToken cancellationToken) =>
        await _settingsPageService.BuildViewModelAsync(
            user,
            BuildLastFmAuthorizationCallbackUri(),
            cancellationToken,
            submission);

    /// <remarks>
    /// Last.fm redirects to this URL after authorizing. Default is inferred from this HTTP request -
    /// set Hosting:PublicBaseUrl when Docker / proxies hide your real HTTPS host.
    /// </remarks>
    private string BuildLastFmAuthorizationCallbackUri()
    {
        var callbackPath = Url.Action("LastFmCallback", "Settings");
        if (string.IsNullOrEmpty(callbackPath))
            throw new InvalidOperationException("Routing could not build /Settings/LastFmCallback.");

        var publicBaseUrl = _configuration[HostingPublicBaseUrlKey]?.Trim();
        if (!string.IsNullOrEmpty(publicBaseUrl))
            return $"{publicBaseUrl.TrimEnd('/')}{callbackPath}";

        return $"{Request.Scheme}://{Request.Host.ToUriComponent()}{Request.PathBase}{callbackPath}";
    }
}
