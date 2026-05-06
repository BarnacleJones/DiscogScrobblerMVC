namespace DiscogScrobblerMVC.Data.Entities;

public class ReleaseGenre
{
    public int ReleaseId { get; set; }
    public Release Release { get; set; } = null!;
    public int GenreId { get; set; }
    public Genre Genre { get; set; } = null!;
}
