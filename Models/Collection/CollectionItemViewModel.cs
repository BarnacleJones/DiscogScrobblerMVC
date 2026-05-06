namespace DiscogScrobblerMVC.Models;

public class CollectionItemViewModel
{
    public int ReleaseId { get; set; }
    public int? DiscogsMasterId { get; set; }
    public string ArtistDisplay { get; set; } = string.Empty;
    public IReadOnlyList<ArtistLinkViewModel> Artists { get; set; } = [];
    public string Album { get; set; } = string.Empty;
    public int? Year { get; set; }
    public DateTime DateAdded { get; set; }
    public string? CoverUrl { get; set; }
}
