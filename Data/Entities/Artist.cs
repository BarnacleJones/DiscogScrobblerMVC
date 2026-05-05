namespace DiscogScrobblerMVC.Data.Entities;

public class Artist
{
    public int Id { get; set; }
    public int? DiscogsArtistId { get; set; }
    public string Name { get; set; } = "";
    public ICollection<Release> Releases { get; set; } = [];
}
