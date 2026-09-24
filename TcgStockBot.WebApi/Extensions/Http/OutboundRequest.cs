namespace TcgStockBot.WebApi.Extensions.Http;

/// <summary>
/// Per-request hints a provider can attach for the outbound pipeline (rate limiting and logs),
/// without knowing anything about proxies or handlers.
/// </summary>
internal static class OutboundRequest
{
    /// <summary>The domain the request counts against in OutboundHttp:RateLimits (defaults to the request host).</summary>
    public static readonly HttpRequestOptionsKey<string> RateLimitKey = new("tcgstockbot.rate-limit-key");

    /// <summary>The provider name shown in the logs (e.g. "Amazon").</summary>
    public static readonly HttpRequestOptionsKey<string> Provider = new("tcgstockbot.provider");

    /// <summary>What the logs show instead of host + path, for URLs that carry a secret in the path (e.g. an Awin feed key).</summary>
    public static readonly HttpRequestOptionsKey<string> LogTarget = new("tcgstockbot.log-target");

    /// <summary>
    /// Tags a request with its provider and the domain it counts against.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="provider">The provider name for logs.</param>
    /// <param name="rateLimitKey">The domain for rate limiting (e.g. "amazon.es").</param>
    /// <param name="logTarget">A safe replacement for host + path in the logs, when the path carries a secret.</param>
    /// <returns>The same request.</returns>
    public static HttpRequestMessage ForProvider(this HttpRequestMessage request, string provider, string rateLimitKey, string? logTarget = null)
    {
        request.Options.Set(Provider, provider);
        request.Options.Set(RateLimitKey, rateLimitKey);
        if (logTarget is not null)
        {
            request.Options.Set(LogTarget, logTarget);
        }

        return request;
    }
}
