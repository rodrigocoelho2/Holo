using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TcgStockBot.WebApi.Extensions.Data;
using TcgStockBot.WebApi.Features;

namespace TcgStockBot.UnitTest;

/// <summary>A data context in a throw-away folder, seeded from an in-memory "Bot" section.</summary>
internal sealed class TestDataContext : IDisposable
{
    public TestDataContext(IDictionary<string, string?>? seed = null)
    {
        Directory = Path.Combine(Path.GetTempPath(), $"tcg-test-{Guid.NewGuid():N}");
        Context = Create(Directory, seed);
    }

    public string Directory { get; }

    public BotDataContext Context { get; }

    public static BotDataContext Create(string directory, IDictionary<string, string?>? seed = null)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(seed ?? new Dictionary<string, string?>()).Build();
        return new BotDataContext(Options.Create(new TcgStockBotOptions { DataDirectory = directory }), configuration, NullLogger<BotDataContext>.Instance);
    }

    public void Dispose()
    {
        if (System.IO.Directory.Exists(Directory))
        {
            System.IO.Directory.Delete(Directory, recursive: true);
        }
    }
}

/// <summary>Returns a canned response and records the requested URI.</summary>
internal sealed class FakeHttpMessageHandler(string body, string mediaType = "application/json", HttpStatusCode status = HttpStatusCode.OK)
    : HttpMessageHandler
{
    public Uri? LastRequestUri { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequestUri = request.RequestUri;
        return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, mediaType) });
    }
}

/// <summary>An IHttpClientFactory that always hands out a client over the given handler.</summary>
internal sealed class FakeHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
}

/// <summary>An IOptionsMonitor that always returns the same value.</summary>
internal sealed class StaticOptionsMonitor<T>(T value) : IOptionsMonitor<T>
{
    public T CurrentValue => value;

    public T Get(string? name) => value;

    public IDisposable? OnChange(Action<T, string?> listener) => null;
}
