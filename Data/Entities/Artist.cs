namespace DiscogScrobblerMVC.Data.Entities;

public class Artist
{
    /// <summary>Increment when adding new properties mapped from Discogs artist payloads.</summary>
    public const int ArtistSchemaVersion = 1;

    public int Id { get; set; }
    public int? DiscogsArtistId { get; set; }
    public string Name { get; set; } = "";

    /// <summary>Artist name to use for Last.fm scrobbling (from <c>artist.getCorrection</c> and local Discogs suffix rules). Set on first scrobble; null until then.</summary>
    public string? LastFmArtistName { get; set; }

    /// <summary>Stale when less than <see cref="ArtistSchemaVersion"/> → queue <c>GET /artists/{id}</c>.</summary>
    public int SchemaVersion { get; set; }
    /// <summary>Discogs artist profile / biography HTML or plain text.</summary>
    public string? DiscogsProfile { get; set; }
    public string? DiscogsImageUrl { get; set; }
    public string? LocalImageFilename { get; set; }
    public string? LocalThumbnailFilename { get; set; }
    public DateTimeOffset? DiscogsDetailsFetchedAt { get; set; }
    public ICollection<Release> Releases { get; set; } = [];
}
