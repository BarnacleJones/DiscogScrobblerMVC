using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Xml.Linq;
using DiscogScrobblerMVC.Models;
using DiscogScrobblerMVC.Services.Interfaces;
using Hqub.Lastfm;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace DiscogScrobblerMVC.Services.LastFm;

public class LastFmOAuthService : ILastFmOAuthService
{
    private const string ApiRoot = "https://ws.audioscrobbler.com/2.0/";
    private readonly LastFmOptions _opts;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<LastFmOAuthService> _logger;

    public LastFmOAuthService(
        IOptions<LastFmOptions> options,
        IHttpClientFactory httpClientFactory,
        ILogger<LastFmOAuthService> logger)
    {
        _opts = options.Value;
        _httpFactory = httpClientFactory;
        _logger = logger;
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_opts.ApiKey) && !string.IsNullOrWhiteSpace(_opts.ApiSecret);

    public async Task<(bool Ready, string? AuthUrl, string? Error)> GetAuthorizationUrlAsync(
        string authorizationCallbackAbsoluteUri,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
            return (false, null, "Last.fm ApiKey / ApiSecret are not configured.");
        if (string.IsNullOrWhiteSpace(authorizationCallbackAbsoluteUri))
            return (false, null, "Last.fm authorization callback URL is missing.");

        try
        {
            var client = new LastfmClient(_opts.ApiKey.Trim(), _opts.ApiSecret.Trim());
            var authUrl = await client.GetWebAuthenticationUrlAsync();

            authUrl = string.IsNullOrWhiteSpace(authUrl)
                ? ""
                : authUrl.Trim();

            authUrl = QueryHelpers.AddQueryString(
                authUrl,
                "cb",
                authorizationCallbackAbsoluteUri.Trim());

            return string.IsNullOrWhiteSpace(authUrl)
                ? (false, null, "Last.fm did not return an authorization URL.")
                : (true, authUrl, null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Last.fm auth.getToken failed.");
            return (false, null, ex.Message);
        }
    }

    public async Task<(bool Ok, string? SessionKey, string? Username, string? Error)> ExchangeTokenAsync(
        string unauthorizedToken,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
            return (false, null, null, "Last.fm is not configured.");
        if (string.IsNullOrWhiteSpace(unauthorizedToken))
            return (false, null, null, "Missing Last.fm authorization token.");

        unauthorizedToken = unauthorizedToken.Trim();
        var apiKey = _opts.ApiKey.Trim();
        var apiSecret = _opts.ApiSecret.Trim();

        var unsigned = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["method"] = "auth.getSession",
            ["token"] = unauthorizedToken,
            ["api_key"] = apiKey,
        };

        var apiSig = Sign(unsigned, apiSecret);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, ApiRoot)
            {
                Content = new FormUrlEncodedContent(
                    unsigned.Select(x => new KeyValuePair<string?, string?>(x.Key, x.Value))
                        .Append(new KeyValuePair<string?, string?>("api_sig", apiSig))),
            };

            request.Headers.TryAddWithoutValidation("User-Agent", "DiscogScrobblerMVC");

            var http = _httpFactory.CreateClient();
            using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var doc = XDocument.Parse(body);
            var lfm = doc.Root;
            var status = lfm?.Attribute("status")?.Value;
            if (status != "ok")
            {
                var errEl = lfm?.Elements().FirstOrDefault(x => x.Name.LocalName == "error");
                var msg = errEl?.Value ?? "Last.fm declined the authorization.";
                return (false, null, null, msg);
            }

            // XML may use a default namespace; match on LocalName.
            var sessionEl = lfm?.Elements().FirstOrDefault(x => x.Name.LocalName == "session")
                ?? doc.Descendants().FirstOrDefault(x => x.Name.LocalName == "session");
            var sessionKey = sessionEl?.Elements().FirstOrDefault(x => x.Name.LocalName == "key")?.Value;
            if (string.IsNullOrWhiteSpace(sessionKey))
                return (false, null, null, "Last.fm response missing session key.");

            var username = sessionEl?.Elements().FirstOrDefault(x => x.Name.LocalName == "name")?.Value;
            return (true, sessionKey.Trim(), string.IsNullOrWhiteSpace(username) ? null : username.Trim(), null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Last.fm auth.getSession failed.");
            return (false, null, null, ex.Message);
        }
    }

    public bool TryExtractTokenFromAuthorizeUrl(string authUrl, [NotNullWhen(true)] out string? token)
    {
        token = null;
        if (!Uri.TryCreate(authUrl, UriKind.Absolute, out var uri))
            return false;
        var q = QueryHelpers.ParseQuery(uri.Query);
        if (!q.TryGetValue("token", out var values))
            return false;
        token = values.FirstOrDefault();
        return !string.IsNullOrWhiteSpace(token);
    }

    private static string Sign(SortedDictionary<string, string> unsignedParams, string apiSecret)
    {
        var sb = new StringBuilder();
        foreach (var kv in unsignedParams)
        {
            sb.Append(kv.Key);
            sb.Append(kv.Value);
        }

        sb.Append(apiSecret);
        return Hqub.Lastfm.MD5.ComputeHash(sb.ToString());
    }
}
