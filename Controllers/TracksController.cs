using DiscogScrobblerMVC.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DiscogScrobblerMVC.Controllers;

[Authorize]
public class TracksController : ApplicationController
{
    private readonly ITrackService _trackService;

    public TracksController(ITrackService trackService)
    {
        _trackService = trackService;
    }

    // GET /Tracks
    public IActionResult Index() => View();

    // GET /Tracks/GetTracks
    // Called by DataTables via AJAX
    [HttpGet]
    public async Task<JsonResult> GetTracks(CancellationToken cancellationToken)
    {
        var trackItems = await _trackService.GetTracksAsync(CurrentUserId, cancellationToken);
        return Json(trackItems);
    }
}
