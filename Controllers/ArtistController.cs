using Microsoft.AspNetCore.Mvc;
using DiscogScrobblerMVC.Services;

namespace DiscogScrobblerMVC.Controllers;

public class ArtistController : Controller
{
    private readonly IArtistService _artistService;

    public ArtistController(IArtistService artistService)
    {
        _artistService = artistService;
    }

    [Route("artist/{id:int}")]
    public async Task<IActionResult> Index(int id)
    {
        var vm = await _artistService.GetArtist(id);
        return vm is null ? NotFound() : View(vm);
    }
}
