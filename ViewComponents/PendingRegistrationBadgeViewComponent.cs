using DiscogScrobblerMVC;
using DiscogScrobblerMVC.Data.Entities;
using DiscogScrobblerMVC.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace DiscogScrobblerMVC.ViewComponents;

public class PendingRegistrationBadgeViewComponent : ViewComponent
{
    private readonly IAccountApprovalService _accountApproval;
    private readonly UserManager<ApplicationUser> _userManager;

    public PendingRegistrationBadgeViewComponent(
        IAccountApprovalService accountApproval,
        UserManager<ApplicationUser> userManager)
    {
        _accountApproval = accountApproval;
        _userManager = userManager;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        if (User.Identity?.IsAuthenticated != true)
            return Content(string.Empty);

        var user = await _userManager.GetUserAsync(HttpContext.User);
        if (user is null)
            return Content(string.Empty);

        var isAdmin = await _userManager.IsInRoleAsync(user, AppRoles.Admin);
        if (!isAdmin)
            return Content(string.Empty);

        var count = await _accountApproval.GetPendingCountAsync(HttpContext.RequestAborted);
        return View(count);
    }
}
