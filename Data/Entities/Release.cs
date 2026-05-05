namespace DiscogScrobblerMVC.Data.Entities;

public class Release
{
    public int Id { get; set; }
    public int DiscogsReleaseId { get; set; }
    public string Artist { get; set; } = "";
    public string Album { get; set; } = "";
    public int Year { get; set; }
    public string? Format { get; set; }
    public string? RecordLabel { get; set; }

    public ICollection<DiscogsReleaseToUser> UserAssociations { get; set; } = [];
    public DiscogsReleaseImages? Images { get; set; }
}
