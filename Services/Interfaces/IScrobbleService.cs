namespace DiscogScrobblerMVC.Services;

public enum ScrobbleFailureReason
{
    None,
    LastFmNotConfigured,
    ReleaseNotFound,
    NotInUserCollection,
    NoTracks,
    LastFmRejected,
}

public interface IScrobbleService
{
    Task<ScrobbleFailureReason> ScrobbleReleaseForUserAsync(
        string userId,
        int discogsReleaseId,
        CancellationToken cancellationToken = default);
}
