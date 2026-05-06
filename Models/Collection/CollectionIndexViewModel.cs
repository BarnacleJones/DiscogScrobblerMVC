namespace DiscogScrobblerMVC.Models;

public class CollectionIndexViewModel
{
    /// <summary>Discogs-formatted currency string (e.g. $100.25), or null if not fetched yet.</summary>
    public string? CollectionValueMin { get; init; }

    public string? CollectionValueMedian { get; init; }
    public string? CollectionValueMax { get; init; }
    public DateTimeOffset? CollectionValueFetchedAt { get; init; }
}
