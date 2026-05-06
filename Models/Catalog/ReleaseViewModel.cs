namespace DiscogScrobblerMVC.Models;

public class ReleaseViewModel
{
    public int ReleaseId { get; set; }
    public int? DiscogsMasterId { get; set; }
    public string Album { get; set; } = string.Empty;
    public int? Year { get; set; }
    public string? CoverUrl { get; set; }
    public int Have { get; set; }
    public int Want { get; set; }
    public IReadOnlyList<ArtistLinkViewModel> Artists { get; set; } = [];
    public IReadOnlyList<LabelLinkViewModel> Labels { get; set; } = [];
    public IReadOnlyList<GenreLinkViewModel> Genres { get; set; } = [];
    public IReadOnlyList<StyleLinkViewModel> Styles { get; set; } = [];
    public IReadOnlyList<TrackViewModel> Tracklist { get; set; } = [];
}

public class TrackViewModel
{
    public string Position { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Duration { get; set; }
}
