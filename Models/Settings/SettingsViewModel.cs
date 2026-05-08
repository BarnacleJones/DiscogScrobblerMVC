using System.ComponentModel.DataAnnotations;

namespace DiscogScrobblerMVC.Models;

public class SettingsViewModel
{
    [Display(Name = "Discogs username")]
    [MaxLength(100)]
    public string? DiscogsUsername { get; set; }

    /// <summary>Optional on POST: leave blank to keep existing token.</summary>
    [DataType(DataType.Password)]
    [Display(Name = "Discogs personal access token")]
    [MaxLength(500)]
    public string? DiscogsPersonalAccessToken { get; set; }

    [Display(Name = "Remove saved Discogs token")]
    public bool ClearDiscogsPersonalAccessToken { get; set; }

    /// <summary>Filled by GET only.</summary>
    public bool HasDiscogsPersonalAccessToken { get; set; }

    /// <summary>Displayed only — filled when the Settings view model is built.</summary>
    [Display(Name = "Last.fm account")]
    public string? LastFmUsername { get; set; }

    public bool LastFmConnected { get; set; }

    /// <summary>Server has ApiKey / Secret so web auth may be started.</summary>
    public bool LastFmConfiguredOnServer { get; set; }

    /// <summary>Unfinished Connect flow — click “Finish” to exchange the token cached on the server.</summary>
    public bool HasPendingLastFmToken { get; set; }

    /// <summary>Callback URI we append as Last.fm <c>cb=</c> plus show in docs.</summary>
    public string LastFmSuggestedCallbackUri { get; set; } = "";

    /// <summary>
    /// Candidate release cover URLs used for the "random covers" preview in Settings.
    /// </summary>
    public List<string> LastFmCoverCandidates { get; set; } = [];

    /// <summary>True when the signed-in user may approve registrations.</summary>
    public bool IsAdmin { get; set; }

    /// <summary>Pending accounts (only populated for admins).</summary>
    public IReadOnlyList<PendingRegistrationViewModel> PendingRegistrations { get; set; } = [];
}

