namespace DiscogScrobblerMVC.Data.Entities;

public class Release
{
    /// <summary>Increment when adding new properties mapped from Discogs release payloads / detail-sync.</summary>
    public const int ReleaseSchemaVersion = 1;

    public int Id { get; set; }
    public int DiscogsReleaseId { get; set; }
    public int? DiscogsMasterId { get; set; }
    public string Album { get; set; } = "";
    public int? Year { get; set; }

    public int SchemaVersion { get; set; }

    public int? CommunityHaveCount { get; set; }
    public int? CommunityWantCount { get; set; }

    public string? Format { get; set; }
    public string? Notes { get; set; }

    public ICollection<Artist> Artists { get; set; } = [];
    public ICollection<Label> Labels { get; set; } = [];
    public ICollection<ReleaseGenre> GenreLinks { get; set; } = [];
    public ICollection<ReleaseStyle> StyleLinks { get; set; } = [];
    public ICollection<Track> Tracks { get; set; } = [];
    public ICollection<DiscogsReleaseToUser> UserAssociations { get; set; } = [];
    public DiscogsReleaseImages? Images { get; set; }
}
