using DiscogScrobblerMVC.Services;
using DiscogScrobblerMVC.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DiscogScrobblerMVC.Controllers;

[Authorize]
public class StylesController : ApplicationController
{
    private readonly ICollectionBrowseService _browseService;

    public StylesController(ICollectionBrowseService browseService)
    {
        _browseService = browseService;
    }

    public IActionResult Index() => View();

    [HttpGet]
    public async Task<JsonResult> GetStyles(CancellationToken cancellationToken)
    {
        var rows = await _browseService.GetStyleReleaseCountsAsync(CurrentUserId, cancellationToken);
        return Json(rows);
    }
}
