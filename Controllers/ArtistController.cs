using DiscogScrobblerMVC.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DiscogScrobblerMVC.Controllers;

[Authorize]
public class ArtistController : Controller
{
    private readonly IArtistService _artistService;

    public ArtistController(IArtistService artistService)
    {
        _artistService = artistService;
    }

    [Route("artist/{id:int}")]
    public async Task<IActionResult> Index(int id, CancellationToken cancellationToken)
    {
        var viewModel = await _artistService.GetArtist(id, cancellationToken);
        return viewModel is null ? NotFound() : View(viewModel);
    }
}