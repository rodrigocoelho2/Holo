using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using TcgStockBot.WebApi.Extensions.Http;

namespace TcgStockBot.WebApi.Features.Stores.Services.Adapters.Amazon;

/// <summary>
/// Gets and caches the Creators API access token (OAuth2 client_credentials, valid 1 hour). Amazon asks clients to
/// cache it for its full lifetime because the token endpoint is rate limited. Thread-safe; one refresh at a time.
/// </summary>
/// <param name="options">Amazon settings.</param>
/// <param name="httpClientFactory">Creates the outbound client.</param>
/// <param name="timeProvider">Clock.</param>
internal sealed class AmazonCreatorsTokenProvider(
    IOptionsMonitor<AmazonCreatorsOptions> options,
    IHttpClientFactory httpClientFactory,
    TimeProvider timeProvider)
{
    private const string ProviderName = "Amazon";
    private static readonly TimeSpan RefreshMargin = TimeSpan.FromSeconds(60);

    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private CachedToken? _cached;

    /// <summary>
    /// Returns a valid access token, requesting a new one only when the cached one is missing or about to expire.
    /// </summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The bearer token.</returns>
    /// <exception cref="StoreIntegrationException">Credentials missing, unsupported version, or rejected.</exception>
    public async Task<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        var cached = Volatile.Read(ref _cached);
        if (cached is not null && cached.ExpiresAt - RefreshMargin > timeProvider.GetUtcNow())
        {
            return cached.AccessToken;
        }

        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            cached = Volatile.Read(ref _cached);
            if (cached is not null && cached.ExpiresAt - RefreshMargin > timeProvider.GetUtcNow())
            {
                return cached.AccessToken;
            }

            var fresh = await RequestTokenAsync(cancellationToken);
            Volatile.Write(ref _cached, fresh);
            return fresh.AccessToken;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    /// <summary>
    /// Drops the cached token (after a 401), so the next call requests a new one.
    /// </summary>
    public void Invalidate() => Volatile.Write(ref _cached, null);

    private async Task<CachedToken> RequestTokenAsync(CancellationToken cancellationToken)
    {
        var settings = options.CurrentValue;
        if (!settings.IsConfigured)
        {
            throw new StoreIntegrationException(ProviderName, StoreIntegrationProblem.NotConfigured,
                "set Amazon__CredentialId, Amazon__CredentialSecret and Amazon__PartnerTag (Associates Central → Creators API).");
        }

        var endpoint = settings.ResolveTokenEndpoint()
            ?? throw new StoreIntegrationException(ProviderName, StoreIntegrationProblem.NotConfigured,
                $"credential version '{settings.CredentialVersion}' is not supported (use 3.1, 3.2 or 3.3; amazon.es = 3.2).");

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(new TokenRequest(settings.CredentialId.Trim(), settings.CredentialSecret.Trim())),
        };
        request.ForProvider(ProviderName, "amazon-auth");

        using var response = await httpClientFactory.CreateClient(AmazonCreatorsStoreAdapter.HttpClientName).SendAsync(request, cancellationToken);

        if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            // The body may echo the client id; never put it in the message.
            throw new StoreIntegrationException(ProviderName, StoreIntegrationProblem.Unauthorized,
                $"the authentication server answered HTTP {(int)response.StatusCode}. Check the Credential ID, the Secret and the version ({settings.CredentialVersion}).");
        }

        response.EnsureSuccessStatusCode();

        var token = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken)
            ?? throw new StoreIntegrationException(ProviderName, StoreIntegrationProblem.ApiError, "empty authentication response.");

        if (string.IsNullOrWhiteSpace(token.AccessToken))
        {
            throw new StoreIntegrationException(ProviderName, StoreIntegrationProblem.ApiError, "the authentication response had no access_token.");
        }

        var lifetime = TimeSpan.FromSeconds(token.ExpiresIn is > 0 ? token.ExpiresIn.Value : 3600);
        return new CachedToken(token.AccessToken, timeProvider.GetUtcNow() + lifetime);
    }

    private sealed record CachedToken(string AccessToken, DateTimeOffset ExpiresAt);

    private sealed record TokenRequest(
        [property: JsonPropertyName("client_id")] string ClientId,
        [property: JsonPropertyName("client_secret")] string ClientSecret)
    {
        [JsonPropertyName("grant_type")]
        public string GrantType => "client_credentials";

        [JsonPropertyName("scope")]
        public string Scope => "creatorsapi::default";
    }

    private sealed class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("expires_in")]
        public int? ExpiresIn { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? Other { get; set; }
    }
}
