using System.Diagnostics;
using System.Net;

namespace TcgStockBot.WebApi.Extensions.Http;

/// <summary>
/// Wraps every outbound store request: waits for the host's turn (throttling), honours Retry-After and logs the
/// outcome (method, host, path, status, duration). Never logs query strings, headers, cookies or bodies.
/// No immediate retries: the monitor already tries again on its next cycle.
/// </summary>
/// <param name="throttle">Per-host throttling.</param>
/// <param name="logger">Logger.</param>
internal sealed class OutboundTrafficHandler(
    DomainThrottle throttle,
    ILogger<OutboundTrafficHandler> logger) : DelegatingHandler
{
    private static readonly TimeSpan DefaultRateLimitBackoff = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Sends the request.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">Cancels the request (includes the HttpClient timeout).</param>
    /// <returns>The response.</returns>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri ?? throw new InvalidOperationException("Outbound request without a URI.");
        var host = uri.Host;
        // No query string (it may carry search terms or keys); providers whose path carries a secret supply a safe label.
        var target = request.Options.TryGetValue(OutboundRequest.LogTarget, out var logTarget) ? logTarget : host + uri.AbsolutePath;
        var rateLimitKey = request.Options.TryGetValue(OutboundRequest.RateLimitKey, out var key) ? key : host;
        var provider = request.Options.TryGetValue(OutboundRequest.Provider, out var name) ? name : host;

        try
        {
            var waited = await throttle.WaitTurnAsync(rateLimitKey, cancellationToken);
            if (waited > TimeSpan.Zero && logger.IsEnabled(LogLevel.Debug))
                logger.LogDebug("Outbound [{Provider}] {RateLimitKey} throttled for {WaitedMs} ms", provider, rateLimitKey, (long)waited.TotalMilliseconds);
        }
        catch (OutboundThrottledException exception)
        {
            if (logger.IsEnabled(LogLevel.Information))
                logger.LogInformation("Outbound [{Provider}] {Method} {Target} skipped: {RateLimitKey} paused for {RetryAfterSeconds} s (rate limit)",
                    provider, request.Method, target, rateLimitKey, (long)exception.RetryAfter.TotalSeconds);
            throw;
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var response = await base.SendAsync(request, cancellationToken);
            var status = (int)response.StatusCode;

            if (response.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable)
            {
                var retryAfter = RetryAfter(response);
                if (retryAfter is not null || response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    throttle.Backoff(rateLimitKey, retryAfter ?? DefaultRateLimitBackoff);
                }
            }

            var level = status >= 400 ? LogLevel.Warning : LogLevel.Information;
            if (logger.IsEnabled(level))
                logger.Log(level, "Outbound [{Provider}] {Method} {Target} -> {StatusCode} in {ElapsedMs} ms",
                    provider, request.Method, target, status, stopwatch.ElapsedMilliseconds);

            return response;
        }
        catch (HttpRequestException exception)
        {
            if (logger.IsEnabled(LogLevel.Warning))
                logger.LogWarning("Outbound [{Provider}] {Method} {Target} failed after {ElapsedMs} ms: {Error}",
                    provider, request.Method, target, stopwatch.ElapsedMilliseconds, exception.HttpRequestError);
            throw;
        }
        catch (OperationCanceledException)
        {
            if (logger.IsEnabled(LogLevel.Warning))
                logger.LogWarning("Outbound [{Provider}] {Method} {Target} failed after {ElapsedMs} ms: Timeout",
                    provider, request.Method, target, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }

    private static TimeSpan? RetryAfter(HttpResponseMessage response)
    {
        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter?.Delta is { } delta)
        {
            return delta;
        }

        if (retryAfter?.Date is { } date)
        {
            var remaining = date - DateTimeOffset.UtcNow;
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }

        return null;
    }
}
