using Microsoft.AspNetCore.Mvc;
using DiscogScrobblerMVC.Services;

namespace DiscogScrobblerMVC.Controllers;

public class LabelController : Controller
{
    private readonly ILabelService _labelService;

    public LabelController(ILabelService labelService)
    {
        _labelService = labelService;
    }

    [Route("label/{id:int}")]
    public async Task<IActionResult> Index(int id)
    {
        var vm = await _labelService.GetLabel(id);
        return vm is null ? NotFound() : View(vm);
    }
}
