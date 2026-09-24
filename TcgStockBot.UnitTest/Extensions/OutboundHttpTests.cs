using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TcgStockBot.WebApi.Extensions.Http;

namespace TcgStockBot.UnitTest.Extensions;

/// <summary>
/// Optional outbound proxy, per-domain throttling, Retry-After and outbound logs. The proxy is a fake one on
/// 127.0.0.1 that records what it receives; nothing leaves the machine.
/// </summary>
public class OutboundHttpTests
{
    [Fact]
    public void ProxyDisabled_GoesDirect_AndIgnoresEnvironmentProxies()
    {
        var options = new OutboundHttpOptions { Proxy = { Enabled = false, Url = "http://price-proxy:3128" } };

        using var handler = OutboundHttpHandlerFactory.Create(options, NullLogger.Instance);

        Assert.False(handler.UseProxy);   // HTTP_PROXY / HTTPS_PROXY are ignored too
        Assert.Null(handler.Proxy);
        Assert.Equal("direct", OutboundHttpHandlerFactory.ResolveRoute(options, NullLogger.Instance, out _).Label);
    }

    [Fact]
    public void ProxyEnabled_WithCredentials_KeepsThemOutOfTheLabel()
    {
        var options = new OutboundHttpOptions { Proxy = { Enabled = true, Url = "http://bot:p%40ss@price-proxy:3128" } };

        var route = OutboundHttpHandlerFactory.ResolveRoute(options, NullLogger.Instance, out var proxy);

        Assert.True(route.ViaProxy);
        Assert.Equal("proxy price-proxy:3128", route.Label);
        Assert.Equal(new Uri("http://price-proxy:3128/"), proxy!.Address);
        var credential = Assert.IsType<NetworkCredential>(proxy.Credentials);
        Assert.Equal("bot", credential.UserName);
        Assert.Equal("p@ss", credential.Password);
    }

    [Theory]
    [InlineData("")]
    [InlineData("price-proxy:3128")]
    [InlineData("https://price-proxy:3128")]
    [InlineData("socks5://price-proxy:1080")]
    public void ProxyEnabled_WithInvalidUrl_FallsBackToDirectAndLogsError(string url)
    {
        var logger = new ListLogger();

        var route = OutboundHttpHandlerFactory.ResolveRoute(new OutboundHttpOptions { Proxy = { Enabled = true, Url = url } }, logger, out var proxy);

        Assert.False(route.ViaProxy);
        Assert.Null(proxy);
        Assert.Contains(logger.Entries, static entry => entry.Level == LogLevel.Error);
    }

    [Fact]
    public async Task ProxyEnabled_HttpRequest_IsSentToTheProxyInAbsoluteForm()
    {
        await using var proxy = FakeProxy.Start("HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Length: 2\r\nConnection: close\r\n\r\nok");
        using var client = CreateClient(new OutboundHttpOptions { Proxy = { Enabled = true, Url = proxy.Url } }, out _);

        var body = await client.GetStringAsync("http://shop.test/products.json?page=1");

        Assert.Equal("ok", body);
        Assert.StartsWith("GET http://shop.test/products.json?page=1 HTTP/1.1", await proxy.RequestHead);
    }

    [Fact]
    public async Task ProxyEnabled_HttpsRequest_OpensAConnectTunnel()
    {
        // The fake proxy refuses the tunnel, so the test stops right after the CONNECT line (no TLS needed).
        await using var proxy = FakeProxy.Start("HTTP/1.1 403 Forbidden\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
        using var client = CreateClient(new OutboundHttpOptions { Proxy = { Enabled = true, Url = proxy.Url } }, out _);

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync("https://www.fnac.pt/"));

        Assert.StartsWith("CONNECT www.fnac.pt:443 HTTP/1.1", await proxy.RequestHead);
        Assert.Equal(HttpRequestError.ProxyTunnelError, exception.HttpRequestError);
    }

    [Fact]
    public async Task ProxyUnavailable_IsReportedAsSuch()
    {
        var closedPort = FakeProxy.FreePort();   // nothing listens here
        using var client = CreateClient(new OutboundHttpOptions { Proxy = { Enabled = true, Url = $"http://127.0.0.1:{closedPort}" } }, out var logger);

        var exception = await Assert.ThrowsAsync<OutboundProxyUnavailableException>(() => client.GetAsync("http://shop.test/"));

        Assert.Equal($"127.0.0.1:{closedPort}", exception.ProxyAuthority);
        Assert.Contains(logger.Entries, static entry => entry.Message.Contains("ProxyUnavailable"));
    }

    [Fact]
    public async Task ProxyThatNeverAnswers_TimesOut()
    {
        await using var proxy = FakeProxy.Start(response: null);   // accepts, reads, never answers
        using var client = CreateClient(new OutboundHttpOptions { Proxy = { Enabled = true, Url = proxy.Url } }, out var logger);
        client.Timeout = TimeSpan.FromMilliseconds(500);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GetAsync("http://shop.test/"));

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
        Assert.Contains(logger.Entries, static entry => entry.Message.StartsWith("Outbound [Amazon] POST creatorsapi.amazon/catalog/v1/searchItems via direct -> 200"));
    }

    [Fact]
    public async Task Logs_NeverContainQueryStringsHeadersOrProxyCredentials()
    {
        await using var proxy = FakeProxy.Start("HTTP/1.1 200 OK\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
        var proxyUrl = proxy.Url.Replace("http://", "http://bot:proxy-password@");
        using var client = CreateClient(new OutboundHttpOptions { Proxy = { Enabled = true, Url = proxyUrl } }, out var logger);

        using var request = new HttpRequestMessage(HttpMethod.Get, "http://shop.test/api/search?apikey=query-secret&q=etb");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "header-secret");
        request.Headers.Add("Cookie", "session=cookie-secret");
        await client.SendAsync(request);

        var logged = string.Join('\n', logger.Entries.Select(static entry => entry.Message));
        Assert.Contains("shop.test/api/search", logged);
        Assert.Contains("via proxy 127.0.0.1:", logged);
        foreach (var secret in new[] { "query-secret", "apikey", "header-secret", "cookie-secret", "proxy-password", "bot:" })
        {
            Assert.DoesNotContain(secret, logged);
        }
    }

    private static HttpClient CreateClient(OutboundHttpOptions options, out ListLogger logger) =>
        CreateClient(options, OutboundHttpHandlerFactory.Create(options, NullLogger.Instance), out logger);

    /// <summary>The same chain as Startup: OutboundTrafficHandler over the given primary handler.</summary>
    private static HttpClient CreateClient(OutboundHttpOptions options, HttpMessageHandler primary, out ListLogger logger)
    {
        logger = new ListLogger();
        var route = OutboundHttpHandlerFactory.ResolveRoute(options, NullLogger.Instance, out _);
        var throttle = new DomainThrottle(new StaticOptionsMonitor<OutboundHttpOptions>(options), TimeProvider.System);
        var traffic = new OutboundTrafficHandler(throttle, route, new TypedLogger<OutboundTrafficHandler>(logger)) { InnerHandler = primary };
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

    /// <summary>A one-connection HTTP proxy on 127.0.0.1 that records the request head and sends a canned answer.</summary>
    private sealed class FakeProxy : IAsyncDisposable
    {
        private readonly TcpListener _listener;
        private readonly CancellationTokenSource _stop = new();

        private FakeProxy(string? response)
        {
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            RequestHead = ServeOneAsync(response);
        }

        public string Url => $"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}";

        public Task<string> RequestHead { get; }

        public static FakeProxy Start(string? response) => new(response);

        public static int FreePort()
        {
            var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            var port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            return port;
        }

        public async ValueTask DisposeAsync()
        {
            await _stop.CancelAsync();
            _listener.Stop();
            _stop.Dispose();
        }

        private async Task<string> ServeOneAsync(string? response)
        {
            using var client = await _listener.AcceptTcpClientAsync(_stop.Token);
            var stream = client.GetStream();
            var head = new StringBuilder();
            var buffer = new byte[1024];

            while (!head.ToString().Contains("\r\n\r\n"))
            {
                var read = await stream.ReadAsync(buffer, _stop.Token);
                if (read == 0)
                {
                    break;
                }

                head.Append(Encoding.ASCII.GetString(buffer, 0, read));
            }

            if (response is null)
            {
                await Task.Delay(Timeout.Infinite, _stop.Token).ContinueWith(static _ => { }, TaskScheduler.Default);
            }
            else
            {
                await stream.WriteAsync(Encoding.ASCII.GetBytes(response), _stop.Token);
            }

            return head.ToString();
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
