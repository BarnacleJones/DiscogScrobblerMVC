namespace DiscogScrobblerMVC.Data.Entities;

public class Track
{
    public int Id { get; set; }
    public int ReleaseId { get; set; }
    public string Position { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Duration { get; set; }

    public Release Release { get; set; } = null!;
}
