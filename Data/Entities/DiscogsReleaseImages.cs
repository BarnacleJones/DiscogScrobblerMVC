namespace DiscogScrobblerMVC.Data.Entities;

public class DiscogsReleaseImages
{
    public int Id { get; set; }
    public int DiscogsReleaseId { get; set; }
    public string? CoverUrl { get; set; }
    public byte[]? CoverImage { get; set; }

    public Release Release { get; set; } = null!;
}
