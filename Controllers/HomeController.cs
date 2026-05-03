using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using DiscogScrobblerMVC.Models;
using DiscogScrobblerMVC.Services;

namespace DiscogScrobblerMVC.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private DiscogsService _discogsService;

    public HomeController(ILogger<HomeController> logger, DiscogsService discogsService)
    {
        _logger = logger;
        _discogsService = discogsService;
    }

    public async Task<IActionResult> Index()
    {
        // In AccountController or wherever your login POST lives
        var userId = "b;a";
        var discogsUsername = /* load from user profile/claim */ "";

        await _discogsService.SyncCollectionAsync(discogsUsername, userId);
        
        return View();
    }


    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
