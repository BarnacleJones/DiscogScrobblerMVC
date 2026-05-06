namespace DiscogScrobblerMVC.Data.Entities;

public class Genre
{
    public int Id { get; set; }
    /// <summary>Display name as supplied by Discogs (first-insert casing wins).</summary>
    public string Name { get; set; } = "";
    /// <summary>Lower invariant key for uniqueness and lookups.</summary>
    public string NormalizedName { get; set; } = "";

    public ICollection<ReleaseGenre> ReleaseLinks { get; set; } = [];
}
