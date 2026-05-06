namespace DiscogScrobblerMVC.Models;

public class HomeIndexViewModel
{
    public bool IsAuthenticated { get; set; }
    public IReadOnlyList<HomeRecentReleaseViewModel> RecentReleases { get; set; } = [];
}

public class HomeRecentReleaseViewModel
{
    public int ReleaseId { get; set; }
    public string Album { get; set; } = "";
    public string ArtistDisplay { get; set; } = "";
    public IReadOnlyList<ArtistLinkViewModel> Artists { get; set; } = [];
    public string? CoverUrl { get; set; }
}
