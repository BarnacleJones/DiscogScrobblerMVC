namespace DiscogScrobblerMVC.Models;

public class RandomReleaseChoiceViewModel
{
    public int ReleaseId { get; set; }
    public string Album { get; set; } = string.Empty;
    public string? CoverUrl { get; set; }
}
