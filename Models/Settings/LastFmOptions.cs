namespace DiscogScrobblerMVC.Models;

public class LastFmOptions
{
    public const string SectionName = "LastFm";

    public string ApiKey { get; set; } = "";
    public string ApiSecret { get; set; } = "";

    /// <summary>
    /// Optional global session key — used only when no user-linked Last.fm account exists.
    /// Prefer linking each identity user under Settings (<c>/Settings</c>) with web auth.
    /// </summary>
    public string? SessionKey { get; set; }

    /// <summary>Legacy fallback: mobile auth (<c>auth.getMobileSession</c>). Prefer web auth instead.</summary>
    public string? Username { get; set; }

    /// <summary>Legacy fallback paired with <see cref="Username"/>.</summary>
    public string? Password { get; set; }
}
