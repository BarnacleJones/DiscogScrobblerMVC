namespace DiscogScrobblerMVC.Models;

public class CollectionBrowseViewModel
{
    /// <summary>Short label for the section header (e.g. "Year", "Genre", "Style").</summary>
    public string DimensionLabel { get; set; } = string.Empty;

    /// <summary>Human-readable value being browsed (e.g. "2005", "Rock").</summary>
    public string ValueTitle { get; set; } = string.Empty;

    public IReadOnlyList<CollectionReleaseCardViewModel> Releases { get; set; } = [];
}
