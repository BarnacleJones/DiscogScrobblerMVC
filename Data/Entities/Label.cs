namespace DiscogScrobblerMVC.Data.Entities;

public class Label
{
    /// <summary>Increment when adding new properties mapped from Discogs label payloads.</summary>
    public const int LabelSchemaVersion = 1;

    public int Id { get; set; }
    public int? DiscogsLabelId { get; set; }
    public string Name { get; set; } = "";

    /// <summary>Stale when less than <see cref="LabelSchemaVersion"/> → queue <c>GET /labels/{id}</c>.</summary>
    public int SchemaVersion { get; set; }
    /// <summary>Discogs label profile text.</summary>
    public string? DiscogsProfile { get; set; }
    public string? DiscogsImageUrl { get; set; }
    public string? LocalImageFilename { get; set; }
    public string? LocalThumbnailFilename { get; set; }
    public DateTimeOffset? DiscogsDetailsFetchedAt { get; set; }
    public ICollection<Release> Releases { get; set; } = [];
}
