using System.Net;
using Microsoft.Extensions.Logging;
using TcgStockBot.WebApi.Extensions.Http;

namespace TcgStockBot.UnitTest.Extensions;

/// <summary>
/// Outbound price-provider traffic: direct connection, per-domain throttling, Retry-After, timeouts and logs
/// without secrets.
/// </summary>
public class OutboundHttpTests
{
    [Fact]
    public void Handler_IsDirect_AndIgnoresEnvironmentProxies()
    {
        using var handler = OutboundHttpHandlerFactory.Create(new OutboundHttpOptions { ConnectTimeout = TimeSpan.FromSeconds(7) });

        Assert.False(handler.UseProxy);   // HTTP_PROXY / HTTPS_PROXY are ignored too
        Assert.Null(handler.Proxy);
        Assert.Equal(TimeSpan.FromSeconds(7), handler.ConnectTimeout);
    }

    [Fact]
    public async Task SlowSite_TimesOutAndIsLogged()
    {
        var options = new OutboundHttpOptions { DefaultRateLimitSeconds = 0 };
        using var client = CreateClient(options, new SlowHandler(), out var logger);
        client.Timeout = TimeSpan.FromMilliseconds(300);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GetAsync("https://shop.test/"));

        Assert.Contains(logger.Entries, static entry => entry.Message.EndsWith("Timeout"));
    }

    [Fact]
    public async Task RateLimit_SecondRequestWithinTheInterval_IsSkippedNotSent()
    {
        var options = new OutboundHttpOptions { RateLimits = { ["fnac.pt"] = 60 }, MaxThrottleWait = TimeSpan.FromSeconds(5) };
        var inner = new CountingHandler(HttpStatusCode.OK);
        using var client = CreateClient(options, inner, out _);

        await client.GetAsync("https://www.fnac.pt/a");
        var exception = await Assert.ThrowsAsync<OutboundThrottledException>(() => client.GetAsync("https://www.fnac.pt/b"));

        Assert.Equal(1, inner.Calls);   // no aggressive retry, no second request
        Assert.InRange(exception.RetryAfter, TimeSpan.FromSeconds(55), TimeSpan.FromSeconds(60));
    }

    [Fact]
    public async Task RateLimit_DifferentDomains_DoNotWaitForEachOther()
    {
        var options = new OutboundHttpOptions { DefaultRateLimitSeconds = 60, MaxThrottleWait = TimeSpan.Zero };
        var inner = new CountingHandler(HttpStatusCode.OK);
        using var client = CreateClient(options, inner, out _);

        await client.GetAsync("https://www.worten.pt/");
        await client.GetAsync("https://www.elcorteingles.es/");

        Assert.Equal(2, inner.Calls);
    }

    [Theory]
    [InlineData("www.amazon.es", 5)]
    [InlineData("amazon.es", 5)]
    [InlineData("static.fnac.pt", 10)]
    [InlineData("shop.fnac.pt", 3)]     // most specific rule wins
    [InlineData("notamazon.es", 2)]     // suffix must be a real subdomain
    [InlineData("trapcard.pt", 2)]      // default
    public void RateLimit_RuleResolution(string host, int expectedSeconds)
    {
        var options = new OutboundHttpOptions
        {
            DefaultRateLimitSeconds = 2,
            RateLimits = new(StringComparer.OrdinalIgnoreCase) { ["amazon.es"] = 5, ["fnac.pt"] = 10, ["shop.fnac.pt"] = 3 },
        };

        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), DomainThrottle.MinIntervalFor(host, options));
    }

    [Fact]
    public async Task TooManyRequests_RetryAfterPausesTheDomain()
    {
        var options = new OutboundHttpOptions { DefaultRateLimitSeconds = 0, MaxThrottleWait = TimeSpan.FromSeconds(1) };
        var inner = new CountingHandler(HttpStatusCode.TooManyRequests, retryAfter: TimeSpan.FromSeconds(120));
        using var client = CreateClient(options, inner, out _);

        var first = await client.GetAsync("https://www.worten.pt/search");
        var exception = await Assert.ThrowsAsync<OutboundThrottledException>(() => client.GetAsync("https://www.worten.pt/search"));

        Assert.Equal(HttpStatusCode.TooManyRequests, first.StatusCode);   // surfaced once, not retried
        Assert.Equal(1, inner.Calls);
        Assert.InRange(exception.RetryAfter, TimeSpan.FromSeconds(115), TimeSpan.FromSeconds(120));
    }

    [Fact]
    public async Task TooManyRequests_RetryAfterIsCappedAtMaxBackoff()
    {
        var options = new OutboundHttpOptions { DefaultRateLimitSeconds = 0, MaxThrottleWait = TimeSpan.Zero, MaxBackoff = TimeSpan.FromMinutes(10) };
        var inner = new CountingHandler(HttpStatusCode.TooManyRequests, retryAfter: TimeSpan.FromDays(2));
        using var client = CreateClient(options, inner, out _);

        await client.GetAsync("https://www.worten.pt/");
        var exception = await Assert.ThrowsAsync<OutboundThrottledException>(() => client.GetAsync("https://www.worten.pt/"));

        Assert.True(exception.RetryAfter <= TimeSpan.FromMinutes(10));
    }

    [Fact]
    public async Task ProviderTag_CountsAnApiHostUnderTheStoreDomain()
    {
        var options = new OutboundHttpOptions { RateLimits = { ["amazon.es"] = 60 }, MaxThrottleWait = TimeSpan.Zero };
        var inner = new CountingHandler(HttpStatusCode.OK);
        using var client = CreateClient(options, inner, out var logger);

        using var first = new HttpRequestMessage(HttpMethod.Post, "https://creatorsapi.amazon/catalog/v1/searchItems").ForProvider("Amazon", "amazon.es");
        await client.SendAsync(first);
        using var second = new HttpRequestMessage(HttpMethod.Post, "https://creatorsapi.amazon/catalog/v1/getItems").ForProvider("Amazon", "amazon.es");

        await Assert.ThrowsAsync<OutboundThrottledException>(() => client.SendAsync(second));
        Assert.Contains(logger.Entries, static entry => entry.Message.StartsWith("Outbound [Amazon] POST creatorsapi.amazon/catalog/v1/searchItems -> 200"));
    }

    [Fact]
    public async Task Logs_NeverContainQueryStringsOrHeaders()
    {
        var options = new OutboundHttpOptions { DefaultRateLimitSeconds = 0 };
        using var client = CreateClient(options, new CountingHandler(HttpStatusCode.OK), out var logger);

        using var request = new HttpRequestMessage(HttpMethod.Get, "https://shop.test/api/search?apikey=query-secret&q=etb");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "header-secret");
        request.Headers.Add("Cookie", "session=cookie-secret");
        await client.SendAsync(request);

        var logged = string.Join('\n', logger.Entries.Select(static entry => entry.Message));
        Assert.Contains("shop.test/api/search", logged);
        foreach (var secret in new[] { "query-secret", "apikey", "header-secret", "cookie-secret" })
        {
            Assert.DoesNotContain(secret, logged);
        }
    }

    /// <summary>The same chain as Startup: OutboundTrafficHandler over the given primary handler.</summary>
    private static HttpClient CreateClient(OutboundHttpOptions options, HttpMessageHandler primary, out ListLogger logger)
    {
        logger = new ListLogger();
        var throttle = new DomainThrottle(new StaticOptionsMonitor<OutboundHttpOptions>(options), TimeProvider.System);
        var traffic = new OutboundTrafficHandler(throttle, new TypedLogger<OutboundTrafficHandler>(logger)) { InnerHandler = primary };
        return new HttpClient(traffic);
    }

    /// <summary>Answers every request with a fixed status and counts the calls.</summary>
    private sealed class CountingHandler(HttpStatusCode status, TimeSpan? retryAfter = null) : HttpMessageHandler
    {
        private int _calls;

        public int Calls => _calls;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _calls);
            var response = new HttpResponseMessage(status);
            if (retryAfter is { } delay)
            {
                response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(delay);
            }

            return Task.FromResult(response);
        }
    }

    /// <summary>Never answers (until cancelled).</summary>
    private sealed class SlowHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}

/// <summary>Records formatted log messages.</summary>
internal sealed class ListLogger : ILogger
{
    public List<(LogLevel Level, string Message)> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        lock (Entries)
        {
            Entries.Add((logLevel, formatter(state, exception)));
        }
    }
}

/// <summary>An ILogger&lt;T&gt; over a <see cref="ListLogger"/>.</summary>
internal sealed class TypedLogger<T>(ListLogger inner) : ILogger<T>
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => inner.BeginScope(state);

    public bool IsEnabled(LogLevel logLevel) => inner.IsEnabled(logLevel);

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        inner.Log(logLevel, eventId, state, exception, formatter);
}
