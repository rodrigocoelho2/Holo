namespace TcgStockBot.WebApi.Extensions.Http;

/// <summary>
/// Outbound HTTP settings for the price providers (connect timeout, per-domain rate limits), bound from the
/// "OutboundHttp" section. In Docker: OutboundHttp__RateLimits__amazon.es, OutboundHttp__DefaultRateLimitSeconds, …
/// </summary>
public sealed class OutboundHttpOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "OutboundHttp";

    /// <summary>Gets or sets how long to wait for the TCP connection to the site.</summary>
    public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Gets or sets the rate limits: domain → MINIMUM NUMBER OF SECONDS between two requests to that domain.
    /// A rule for "amazon.es" also covers "www.amazon.es". Providers that call an API on another host
    /// (e.g. the Amazon Creators API) count their calls under their store's domain.
    /// </summary>
    public Dictionary<string, int> RateLimits { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets or sets the minimum seconds between two requests to the same domain when no rule matches.</summary>
    public int DefaultRateLimitSeconds { get; set; } = 2;

    /// <summary>Gets or sets the longest a request waits for its turn; beyond this it is skipped ("adiado") instead of hanging.</summary>
    public TimeSpan MaxThrottleWait { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Gets or sets the cap applied to a site's Retry-After (429/503).</summary>
    public TimeSpan MaxBackoff { get; set; } = TimeSpan.FromHours(1);
}
