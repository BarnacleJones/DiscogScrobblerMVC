namespace DiscogScrobblerMVC.Data.Entities;

public class DiscogsReleaseToUser
{
    public int Id { get; set; }
    public int DiscogsReleaseId { get; set; }
    /// <summary>Discogs collection folder id when known.</summary>
    public int? DiscogsFolderId { get; set; }
    /// <summary>Discogs instance id — unique per copy. Null on legacy rows until merged on sync.</summary>
    public int? DiscogsInstanceId { get; set; }
    public string UserId { get; set; } = "";
    public DateTime DateAdded { get; set; }

    public Release Release { get; set; } = null!;
}
