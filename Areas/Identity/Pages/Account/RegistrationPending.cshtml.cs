using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DiscogScrobblerMVC.Areas.Identity.Pages.Account;

[AllowAnonymous]
public class RegistrationPendingModel : PageModel
{
    public void OnGet()
    {
    }
}
