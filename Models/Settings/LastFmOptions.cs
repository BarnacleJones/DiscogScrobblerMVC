namespace DiscogScrobblerMVC.Models;

/// <summary>Last.fm API application credentials (<c>ApiKey</c>/<c>ApiSecret</c>). User write access uses OAuth session keys stored per Identity user.</summary>
public class LastFmOptions
{
    public const string SectionName = "LastFm";

    public string ApiKey { get; set; } = "";
    public string ApiSecret { get; set; } = "";
}
