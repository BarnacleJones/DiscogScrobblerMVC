using System.Reflection;
using DiscogsApiClient.Authentication;

namespace DiscogScrobblerMVC.Services.Discogs;

/// <summary>
/// DiscogsApiClient keeps the personal access token in process wide state and exposes no public logout;
/// For username-only sync we must drop that state so <see cref="AuthenticationDelegatingHandler"/> omits the Authorization header.
/// This relies on DiscogsApiClient implementation details (v4.1.0); if an upgrade breaks it, see logs and adjust field names.
/// </summary>
internal static class DiscogsAuthenticationAnonymousHelper
{
    private const BindingFlags InstanceNonPublic = BindingFlags.Instance | BindingFlags.NonPublic;

    public static void ClearPersonalAccessStateForAnonymousRequests(
        IDiscogsAuthenticationService authenticationService,
        ILogger logger)
    {
        var implementationType = authenticationService.GetType();
        if (implementationType.Name != "DiscogsAuthenticationService")
        {
            logger.LogWarning(
                "Unexpected {Interface} implementation {Type}; cannot clear PAT — public-only Discogs calls may use the wrong identity.",
                nameof(IDiscogsAuthenticationService),
                implementationType.FullName);
            return;
        }

        try
        {
            var patProvider = implementationType
                .GetField("_personalAccessTokenAuthenticationProvider", InstanceNonPublic)?
                .GetValue(authenticationService);
            if (patProvider is null)
                return;

            patProvider.GetType().GetField("_userToken", InstanceNonPublic)?.SetValue(patProvider, string.Empty);

            implementationType
                .GetField("_lastAuthenticatedWithPersonalAccessToken", InstanceNonPublic)?
                .SetValue(authenticationService, false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to clear Discogs personal access token state before anonymous API calls.");
        }
    }
}
