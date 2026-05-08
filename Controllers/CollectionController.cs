using DiscogScrobblerMVC.Data.Entities;
using DiscogScrobblerMVC.Models;
using DiscogScrobblerMVC.Services;
using DiscogScrobblerMVC.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace DiscogScrobblerMVC.Controllers;

[Authorize]
public class CollectionController : ApplicationController
{
    private readonly ICollectionService _collectionService;
    private readonly IScrobbleService _scrobbleService;
    private readonly UserManager<ApplicationUser> _userManager;

    public CollectionController(
        ICollectionService collectionService,
        IScrobbleService scrobbleService,
        UserManager<ApplicationUser> userManager)
    {
        _collectionService = collectionService;
        _scrobbleService = scrobbleService;
        _userManager = userManager;
    }

    // GET /Collection
    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
            return Challenge();

        var viewModel = new CollectionIndexViewModel
        {
            CollectionValueMin = user.DiscogsCollectionValueMin,
            CollectionValueMedian = user.DiscogsCollectionValueMedian,
            CollectionValueMax = user.DiscogsCollectionValueMax,
            CollectionValueFetchedAt = user.DiscogsCollectionValueFetchedAt,
        };

        return View(viewModel);
    }

    // GET /Collection/Search?q=
    [HttpGet]
    public async Task<IActionResult> Search([FromQuery(Name = "q")] string? query, CancellationToken cancellationToken)
    {
        ViewData["SearchQuery"] = query ?? "";
        var viewModel = await _collectionService.SearchCollectionAsync(CurrentUserId, query, cancellationToken);
        ViewData["Title"] = string.IsNullOrWhiteSpace(viewModel.Query) ? "Search" : $"Search — {viewModel.Query}";
        return View(viewModel);
    }

    // GET /Collection/GetCollection
    // Called by DataTables via AJAX
    [HttpGet]
    public async Task<JsonResult> GetCollection(CancellationToken cancellationToken)
    {
        var collectionItems = await _collectionService.GetCollectionItemsAsync(CurrentUserId, cancellationToken);
        return Json(collectionItems);
    }

    // POST /Collection/Scrobble
    [HttpPost]
    public async Task<IActionResult> Scrobble(int releaseId, CancellationToken cancellationToken)
    {
        var scrobbleOutcome = await _scrobbleService.ScrobbleReleaseForUserAsync(
            CurrentUserId, releaseId, cancellationToken);
        return ToScrobbleActionResult(scrobbleOutcome);
    }
}
