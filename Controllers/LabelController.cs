using DiscogScrobblerMVC.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DiscogScrobblerMVC.Controllers;

[Authorize]
public class LabelController : Controller
{
    private readonly ILabelService _labelService;

    public LabelController(ILabelService labelService)
    {
        _labelService = labelService;
    }

    [Route("label/{id:int}")]
    public async Task<IActionResult> Index(int id, CancellationToken cancellationToken)
    {
        var viewModel = await _labelService.GetLabel(id, cancellationToken);
        return viewModel is null ? NotFound() : View(viewModel);
    }
}
