using Microsoft.AspNetCore.Identity;

namespace DiscogScrobblerMVC.Data.Entities;

public class ApplicationUser : IdentityUser
{
    public string? DiscogsUsername { get; set; }
}