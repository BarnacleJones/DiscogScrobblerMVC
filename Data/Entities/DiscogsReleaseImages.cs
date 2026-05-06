namespace DiscogScrobblerMVC.Data.Entities;

public class DiscogsReleaseImages
{
    public int Id { get; set; }
    public int DiscogsReleaseId { get; set; }
    // Original remote cover URL from Discogs.
    public string? CoverUrl { get; set; }
    
    // Full-size locally cached image filename (stored under App:ImageBasePath).
    public string? LocalImageFilename { get; set; }
    // Smaller locally generated image filename used by grids/lists.
    public string? LocalThumbnailFilename { get; set; }

    public Release Release { get; set; } = null!;
}
