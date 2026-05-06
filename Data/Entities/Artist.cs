namespace DiscogScrobblerMVC.Data.Entities;

public class Artist
{
    public int Id { get; set; }
    public int? DiscogsArtistId { get; set; }
    public string Name { get; set; } = "";
    /// <summary>Discogs artist profile / biography HTML or plain text.</summary>
    public string? DiscogsProfile { get; set; }
    public string? DiscogsImageUrl { get; set; }
    public string? LocalImageFilename { get; set; }
    public string? LocalThumbnailFilename { get; set; }
    public DateTimeOffset? DiscogsDetailsFetchedAt { get; set; }
    public ICollection<Release> Releases { get; set; } = [];
}
