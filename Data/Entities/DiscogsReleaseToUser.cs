namespace DiscogScrobblerMVC.Data.Entities;

public class DiscogsReleaseToUser
{
    public int Id { get; set; }
    public int DiscogsReleaseId { get; set; }
    public string UserId { get; set; } = "";
    public DateTime DateAdded { get; set; }

    public Release Release { get; set; } = null!;
}
