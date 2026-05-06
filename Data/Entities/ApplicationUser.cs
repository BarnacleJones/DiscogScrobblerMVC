using Microsoft.AspNetCore.Identity;

namespace DiscogScrobblerMVC.Data.Entities;

public class ApplicationUser : IdentityUser
{
    public string? DiscogsUsername { get; set; }
    /// <summary>Cached Discogs GET /users/.../collection/value (owner auth).</summary>
    public string? DiscogsCollectionValueMin { get; set; }
    public string? DiscogsCollectionValueMedian { get; set; }
    public string? DiscogsCollectionValueMax { get; set; }
    public DateTimeOffset? DiscogsCollectionValueFetchedAt { get; set; }
    /// <summary>Last.fm username from auth.getSession (for display only).</summary>
    public string? LastFmUsername { get; set; }
    /// <summary>Stored session key — write-capable Last.fm authentication for scrobbling.</summary>
    public string? LastFmSessionKey { get; set; }
}