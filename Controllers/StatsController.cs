using DiscogScrobblerMVC.Services;
using DiscogScrobblerMVC.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DiscogScrobblerMVC.Controllers;

[Authorize]
public class StatsController : ApplicationController
{
    private readonly IStatsService _statsService;

    public StatsController(IStatsService statsService)
    {
        _statsService = statsService;
    }

    // GET /Stats
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var viewModel = await _statsService.GetStatsAsync(CurrentUserId, cancellationToken);
        return View(viewModel);
    }
}
