namespace DiscogScrobblerMVC.Models;

public class ReleaseViewModel
{
    public int ReleaseId { get; set; }
    public string Artist { get; set; } = string.Empty;
    public string Album { get; set; } = string.Empty;
    public int Year { get; set; }
    public string CoverUrl { get; set; } = string.Empty;
    public int Have { get; set; }
    public int Want { get; set; }
    public string RecordCompanies { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
    public List<string> Genres { get; set; } = new();
    public List<string> Styles { get; set; } = new();
    public List<TrackViewModel> Tracklist { get; set; } = new();
}

public class TrackViewModel
{
    public string Position { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Duration { get; set; } = string.Empty;
}
