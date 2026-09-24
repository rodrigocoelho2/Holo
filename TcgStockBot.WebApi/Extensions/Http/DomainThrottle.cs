using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace TcgStockBot.WebApi.Extensions.Http;

/// <summary>
/// Spaces out requests to the same host (minimum interval per domain) and pauses a host after it asks us to
/// slow down (429/503 with Retry-After). Each call reserves the next free slot for its host, then waits for it
/// outside the lock; different hosts never wait for each other.
/// </summary>
/// <param name="options">Outbound settings (read on every call, so changes apply without restart).</param>
/// <param name="timeProvider">Clock.</param>
internal sealed class DomainThrottle(IOptionsMonitor<OutboundHttpOptions> options, TimeProvider timeProvider)
{
    private readonly ConcurrentDictionary<string, HostState> _hosts = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Waits until the host may be asked. The slot is reserved before waiting, so concurrent callers queue up in order.
    /// </summary>
    /// <param name="host">The target host.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>How long the call waited.</returns>
    /// <exception cref="OutboundThrottledException">The wait would exceed MaxThrottleWait; no slot is taken.</exception>
    public async Task<TimeSpan> WaitTurnAsync(string host, CancellationToken cancellationToken)
    {
        var settings = options.CurrentValue;
        var state = _hosts.GetOrAdd(host, static _ => new HostState());
        TimeSpan wait;

        lock (state)
        {
            var now = timeProvider.GetUtcNow();
            var start = state.NextAllowed > now ? state.NextAllowed : now;
            wait = start - now;

            if (wait > settings.MaxThrottleWait)
            {
                throw new OutboundThrottledException(host, wait);
            }

            state.NextAllowed = start + MinIntervalFor(host, settings);
        }

        if (wait > TimeSpan.Zero)
        {
            await Task.Delay(wait, timeProvider, cancellationToken);
        }

        return wait;
    }

    /// <summary>
    /// Pauses a host (e.g. after 429 Retry-After), capped at MaxBackoff.
    /// </summary>
    /// <param name="host">The host.</param>
    /// <param name="duration">How long to pause.</param>
    public void Backoff(string host, TimeSpan duration)
    {
        var settings = options.CurrentValue;
        var capped = duration > settings.MaxBackoff ? settings.MaxBackoff : duration;
        var state = _hosts.GetOrAdd(host, static _ => new HostState());

        lock (state)
        {
            var until = timeProvider.GetUtcNow() + capped;
            if (until > state.NextAllowed)
            {
                state.NextAllowed = until;
            }
        }
    }

    /// <summary>
    /// The interval that applies to a host: the most specific matching rule, or the default.
    /// </summary>
    /// <param name="host">The host.</param>
    /// <param name="settings">The settings.</param>
    /// <returns>The minimum interval.</returns>
    internal static TimeSpan MinIntervalFor(string host, OutboundHttpOptions settings)
    {
        var rule = settings.RateLimits
            .Where(rule => !string.IsNullOrWhiteSpace(rule.Key)
                && (host.Equals(rule.Key, StringComparison.OrdinalIgnoreCase)
                    || host.EndsWith("." + rule.Key, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(static rule => rule.Key.Length)
            .Select(static rule => (int?)rule.Value)
            .FirstOrDefault();

        var seconds = rule ?? settings.DefaultRateLimitSeconds;
        return seconds > 0 ? TimeSpan.FromSeconds(seconds) : TimeSpan.Zero;
    }

    private sealed class HostState
    {
        public DateTimeOffset NextAllowed { get; set; } = DateTimeOffset.MinValue;
    }
}
