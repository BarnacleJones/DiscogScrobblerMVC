namespace DiscogScrobblerMVC.Data.Entities;

public class Release
{
    public int Id { get; set; }
    public int DiscogsReleaseId { get; set; }
    public string Album { get; set; } = "";
    public int Year { get; set; }
    public string? Genres { get; set; }
    public string? Styles { get; set; }

    public ICollection<Artist> Artists { get; set; } = [];
    public ICollection<Label> Labels { get; set; } = [];
    public ICollection<Track> Tracks { get; set; } = [];
    public ICollection<DiscogsReleaseToUser> UserAssociations { get; set; } = [];
    public DiscogsReleaseImages? Images { get; set; }
}
