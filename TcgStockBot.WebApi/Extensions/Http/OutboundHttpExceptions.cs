namespace TcgStockBot.WebApi.Extensions.Http;

/// <summary>
/// The request was not sent because this host is throttled for longer than the allowed wait
/// (e.g. the store answered 429 with a Retry-After).
/// </summary>
/// <param name="host">The throttled host.</param>
/// <param name="retryAfter">How long until the host may be asked again.</param>
public sealed class OutboundThrottledException(string host, TimeSpan retryAfter)
    : HttpRequestException($"Requests to {host} are paused for {retryAfter.TotalSeconds:0} s.")
{
    /// <summary>Gets the throttled host.</summary>
    public string Host { get; } = host;

    /// <summary>Gets how long until the host may be asked again.</summary>
    public TimeSpan RetryAfter { get; } = retryAfter;
}
