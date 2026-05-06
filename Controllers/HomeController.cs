using System.Diagnostics;
using DiscogScrobblerMVC.Models;
using DiscogScrobblerMVC.Services;
using Microsoft.AspNetCore.Mvc;

namespace DiscogScrobblerMVC.Controllers;

public class HomeController : ApplicationController
{
    private const int RecentReleaseCount = 9;

    private readonly ILogger<HomeController> _logger;
    private readonly ICollectionService _collectionService;

    public HomeController(ILogger<HomeController> logger, ICollectionService collectionService)
    {
        _logger = logger;
        _collectionService = collectionService;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var isAuthenticated = User.Identity?.IsAuthenticated == true;
        var viewModel = new HomeIndexViewModel { IsAuthenticated = isAuthenticated };

        if (isAuthenticated)
        {
            viewModel.RecentReleases = await _collectionService.GetRecentCollectionReleasesAsync(
                CurrentUserId, RecentReleaseCount, cancellationToken);
        }

        return View(viewModel);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
