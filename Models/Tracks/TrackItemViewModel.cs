namespace DiscogScrobblerMVC.Models;

public class TrackItemViewModel
{
    public int ReleaseId { get; set; }
    public string ArtistDisplay { get; set; } = "";
    public string Album { get; set; } = "";
    public int? Year { get; set; }

    public string Position { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Duration { get; set; }
}

