using DiscogScrobblerMVC.Services;
using DiscogScrobblerMVC.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DiscogScrobblerMVC.Controllers;

[Authorize]
public class LabelsController : ApplicationController
{
    private readonly ICollectionBrowseService _browseService;

    public LabelsController(ICollectionBrowseService browseService)
    {
        _browseService = browseService;
    }

    public IActionResult Index() => View();

    [HttpGet]
    public async Task<JsonResult> GetLabels(CancellationToken cancellationToken)
    {
        var rows = await _browseService.GetLabelReleaseCountsAsync(CurrentUserId, cancellationToken);
        return Json(rows);
    }
}
