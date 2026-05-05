using Microsoft.AspNetCore.Mvc;
using DiscogScrobblerMVC.Services;

namespace DiscogScrobblerMVC.Controllers;

public class ReleaseController : Controller
{
    private readonly IReleaseService _releaseService;

    public ReleaseController(IReleaseService releaseService)
    {
        _releaseService = releaseService;
    }

    [Route("release/{id:int}")]
    public async Task<IActionResult> Index(int id)
    {
        var vm = await _releaseService.GetRelease(id);
        return vm is null ? NotFound() : View(vm);
    }

    [HttpPost]
    public IActionResult Scrobble(int releaseId)
    {
        // TODO: call Last.fm scrobble service
        return Ok();
    }
}
