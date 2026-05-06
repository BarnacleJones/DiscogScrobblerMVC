using DiscogScrobblerMVC.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DiscogScrobblerMVC.Controllers;

[Authorize]
public class ReleaseController : ApplicationController
{
    private const int RandomDiceChoiceCount = 5;

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
        var viewModel = await _releaseService.GetRelease(id, cancellationToken);
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
        var diceChoices = await _releaseService.GetRandomReleaseChoicesForUser(
            CurrentUserId, RandomDiceChoiceCount, cancellationToken);
        return Json(diceChoices);
    }

    [HttpPost]
    public async Task<IActionResult> Scrobble(int releaseId, CancellationToken cancellationToken)
    {
        var scrobbleOutcome = await _scrobbleService.ScrobbleReleaseForUserAsync(
            CurrentUserId, releaseId, cancellationToken);
        return ToScrobbleActionResult(scrobbleOutcome);
    }
}
