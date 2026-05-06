namespace DiscogScrobblerMVC.Data.Entities;

public class Track
{
    public int Id { get; set; }
    public int ReleaseId { get; set; }
    public string Position { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Duration { get; set; }
    /// <summary>
    /// Pre-parsed track length in seconds, derived from <see cref="Duration"/> at write time.
    /// Null when Discogs supplied no duration (or it was unparseable). Avoids parsing
    /// "MM:SS"/"HH:MM:SS" strings inside SQL aggregations on the stats page.
    /// </summary>
    public int? DurationSeconds { get; set; }

    public Release Release { get; set; } = null!;
}
