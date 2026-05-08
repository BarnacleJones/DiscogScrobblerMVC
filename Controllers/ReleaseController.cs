using DiscogScrobblerMVC.Services;
using DiscogScrobblerMVC.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DiscogScrobblerMVC.Controllers;

[Authorize]
public class ReleaseController : ApplicationController
{
    private readonly IReleaseService _releaseService;
    private readonly IScrobbleService _scrobbleService;

    public ReleaseController(IReleaseService releaseService, IScrobbleService scrobbleService)
    {
        _releaseService = releaseService;
        _scrobbleService = scrobbleService;
    }

    [Route("release/{id:int}")]
    public async Task<IActionResult> Index(int id, CancellationToken cancellationToken)
    {
        var viewModel = await _releaseService.GetRelease(id, CurrentUserId, cancellationToken);
        return viewModel is null ? NotFound() : View(viewModel);
    }

    [HttpGet("release/random")]
    public async Task<IActionResult> Random(CancellationToken cancellationToken)
    {
        var viewModel = await _releaseService.GetRandomReleaseForUser(CurrentUserId, cancellationToken);
        return View(viewModel);
    }

    [HttpGet("release/random/dice")]
    public async Task<IActionResult> RandomDice(CancellationToken cancellationToken)
    {
        var diceFace = System.Random.Shared.Next(1, 7);
        var releaseChoices = await _releaseService.GetRandomReleaseChoicesForUser(
            CurrentUserId, diceFace, cancellationToken);
        return Json(new { face = diceFace, choices = releaseChoices });
    }

    [HttpPost]
    public async Task<IActionResult> Scrobble(int releaseId, CancellationToken cancellationToken)
    {
        var scrobbleOutcome = await _scrobbleService.ScrobbleReleaseForUserAsync(
            CurrentUserId, releaseId, cancellationToken);
        return ToScrobbleActionResult(scrobbleOutcome);
    }
}
