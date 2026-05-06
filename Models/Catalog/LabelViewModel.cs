namespace DiscogScrobblerMVC.Models;

public class LabelViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Profile { get; set; }
    public string? ImageUrl { get; set; }
    public List<CollectionReleaseCardViewModel> CollectionReleases { get; set; } = [];
}
