using System.Diagnostics.CodeAnalysis;

namespace DiscogScrobblerMVC.Services;

public interface ILastFmOAuthService
{
    /// <summary>
    /// Returns the Last.fm web authorization URL, or false if ApiKey / ApiSecret are missing.
    /// Caller should parse the <c>token</c> query param from the URL and store it alongside the redirect.
    /// </summary>
    /// <param name="authorizationCallbackAbsoluteUri">
    /// After the user taps Allow, Last.fm redirects here (typically <c>?token=...</c>). Pass the canonical public URL —
    /// set <see cref="Hosting:PublicBaseUrl"/> when Docker / reverse proxies hide the browser-visible origin.
    /// </param>
    Task<(bool Ready, string? AuthUrl, string? Error)> GetAuthorizationUrlAsync(
        string authorizationCallbackAbsoluteUri,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Exchanges the unauthorized token from the first step for a session key and Last.fm username.
    /// </summary>
    Task<(bool Ok, string? SessionKey, string? Username, string? Error)> ExchangeTokenAsync(
        string unauthorizedToken,
        CancellationToken cancellationToken = default);

    bool IsConfigured { get; }

    bool TryExtractTokenFromAuthorizeUrl(string authUrl, [NotNullWhen(true)] out string? token);
}
