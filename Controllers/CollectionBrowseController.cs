using DiscogScrobblerMVC.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DiscogScrobblerMVC.Controllers;

[Authorize]
[Route("collection")]
public class CollectionBrowseController : ApplicationController
{
    private readonly ICollectionBrowseService _browseService;

    public CollectionBrowseController(ICollectionBrowseService browseService)
    {
        _browseService = browseService;
    }

    [HttpGet("year/{year:int}")]
    public async Task<IActionResult> Year(int year, CancellationToken cancellationToken)
    {
        var viewModel = await _browseService.GetByYearAsync(CurrentUserId, year, cancellationToken);
        return View("Index", viewModel);
    }

    [HttpGet("genre/{id:int}")]
    public async Task<IActionResult> Genre(int id, CancellationToken cancellationToken)
    {
        var viewModel = await _browseService.GetByGenreIdAsync(CurrentUserId, id, cancellationToken);
        return viewModel is null ? NotFound() : View("Index", viewModel);
    }

    [HttpGet("style/{id:int}")]
    public async Task<IActionResult> Style(int id, CancellationToken cancellationToken)
    {
        var viewModel = await _browseService.GetByStyleIdAsync(CurrentUserId, id, cancellationToken);
        return viewModel is null ? NotFound() : View("Index", viewModel);
    }
}
