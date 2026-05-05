namespace DiscogScrobblerMVC.Models;

public class ReleaseViewModel
{
    public int ReleaseId { get; set; }
    public string Album { get; set; } = string.Empty;
    public int Year { get; set; }
    public string? CoverUrl { get; set; }
    public int Have { get; set; }
    public int Want { get; set; }
    public List<ArtistLinkViewModel> Artists { get; set; } = [];
    public List<LabelLinkViewModel> Labels { get; set; } = [];
    public List<string> Genres { get; set; } = [];
    public List<string> Styles { get; set; } = [];
    public List<TrackViewModel> Tracklist { get; set; } = [];
}

public class TrackViewModel
{
    public string Position { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Duration { get; set; }
}
