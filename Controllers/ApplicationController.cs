using System.Security.Claims;
using DiscogScrobblerMVC.Services;
using Microsoft.AspNetCore.Mvc;

namespace DiscogScrobblerMVC.Controllers;

public abstract class ApplicationController : Controller
{
    protected string CurrentUserId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("Authenticated user id claim is missing.");

    protected IActionResult ToScrobbleActionResult(ScrobbleFailureReason failureReason)
    {
        return failureReason switch
        {
            ScrobbleFailureReason.None => Ok(),
            ScrobbleFailureReason.LastFmNotConfigured => StatusCode(StatusCodes.Status503ServiceUnavailable),
            ScrobbleFailureReason.ReleaseNotFound or ScrobbleFailureReason.NotInUserCollection => NotFound(),
            ScrobbleFailureReason.NoTracks => BadRequest(),
            ScrobbleFailureReason.LastFmRejected => StatusCode(StatusCodes.Status502BadGateway),
            _ => StatusCode(StatusCodes.Status500InternalServerError),
        };
    }
}
