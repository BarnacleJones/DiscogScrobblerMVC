using DiscogScrobblerMVC.Services;
using DiscogScrobblerMVC.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DiscogScrobblerMVC.Controllers;

[Authorize]
public class GenresController : ApplicationController
{
    private readonly ICollectionBrowseService _browseService;

    public GenresController(ICollectionBrowseService browseService)
    {
        _browseService = browseService;
    }

    public IActionResult Index() => View();

    [HttpGet]
    public async Task<JsonResult> GetGenres(CancellationToken cancellationToken)
    {
        var rows = await _browseService.GetGenreReleaseCountsAsync(CurrentUserId, cancellationToken);
        return Json(rows);
    }
}
