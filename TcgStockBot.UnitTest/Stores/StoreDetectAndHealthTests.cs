using System.Net;
using System.Text;
using TcgStockBot.Library.Enums;
using TcgStockBot.Library.Services;
using TcgStockBot.WebApi.Extensions.Globalization;
using TcgStockBot.WebApi.Features.StockTasks.Models.Entities;
using TcgStockBot.WebApi.Features.StockTasks.Repositories;
using TcgStockBot.WebApi.Features.Stores.Models.DTOs;
using TcgStockBot.WebApi.Features.Stores.Models.Entities;
using TcgStockBot.WebApi.Features.Stores.Repositories;
using TcgStockBot.WebApi.Features.Stores.Services;
using TcgStockBot.WebApi.Features.Stores.Services.Adapters;

namespace TcgStockBot.UnitTest.Stores;

/// <summary>
/// Platform detection from public endpoints, store tests kept on the store, and the automatic turn-off after
/// failed cycles.
/// </summary>
public sealed class StoreDetectAndHealthTests : IDisposable
{
    private readonly TestDataContext _data = new();
    private readonly StoreRepository _stores;
    private readonly StockTaskRepository _tasks;

    public StoreDetectAndHealthTests()
    {
        _stores = new StoreRepository(_data.Context);
        _tasks = new StockTaskRepository(_data.Context);
    }

    public void Dispose() => _data.Dispose();

    private static async Task<StoreDetectResult> DetectAsync(Dictionary<string, (HttpStatusCode Status, string Body)> routes, string url = "loja.example")
    {
        var service = new StoreDetectService(new FakeHttpClientFactory(new RoutingHandler(routes)), new GlobalizationHelper());
        var response = await service.DetectAsync(new StoreDetectRequest { Url = url });
        Assert.Equal(ServiceApiResponseStatus.Success, response.Status);
        return response.Result!;
    }

    private const string Home = """<html><head><meta property="og:site_name" content="Loja &amp; Cartas"><title>Início | Loja</title></head></html>""";

    [Fact]
    public async Task Detect_Shopify_WithTheCollectionFromTheAddress()
    {
        var result = await DetectAsync(new()
        {
            ["/"] = (HttpStatusCode.OK, Home),
            ["/products.json?limit=1"] = (HttpStatusCode.OK, """{"products":[{"id":1}]}"""),
            ["/collections/pokemon-tcg-1/products.json?limit=1"] = (HttpStatusCode.OK, """{"products":[{"id":1}]}"""),
        }, "https://loja.example/collections/pokemon-tcg-1");

        Assert.Equal((StoreDetectStatus.Detected, StorePlatform.Shopify), (result.Status, result.Platform));
        Assert.Equal("/collections/pokemon-tcg-1", result.ShopifyCatalogPaths);
        Assert.Equal("Loja & Cartas", result.SuggestedName);
        Assert.Equal("https://loja.example", result.BaseUrl);
    }

    [Fact]
    public async Task Detect_WooCommerce_OnTheSecondPath()
    {
        var result = await DetectAsync(new()
        {
            ["/"] = (HttpStatusCode.OK, "<title>Loja WC – Pokémon</title>"),
            ["/wp-json/wc/store/products?per_page=1"] = (HttpStatusCode.OK, "[{\"id\":1}]"),
        });

        Assert.Equal(StorePlatform.WooCommerce, result.Platform);
        Assert.Equal("/wp-json/wc/store/products", result.WooCommerceProductsPath);
        Assert.Equal("Loja WC", result.SuggestedName);
    }

    [Fact]
    public async Task Detect_PrestaShop_SuggestsTheGenericSelectors()
    {
        var result = await DetectAsync(new()
        {
            ["/"] = (HttpStatusCode.OK, "<title>Presta</title>"),
            ["/index.php?controller=search&s=pokemon"] = (HttpStatusCode.OK, "<article class=\"product-miniature js-product-miniature\"></article>"),
        });

        Assert.Equal(StorePlatform.Html, result.Platform);
        Assert.Equal("https://loja.example/index.php?controller=search&s={query}", result.Html!.SearchUrlTemplate);
        Assert.Equal(".product-miniature", result.Html.ProductSelector);
    }

    [Fact]
    public async Task Detect_Blocked_StopsAtTheHomePage()
    {
        var handler = new RoutingHandler(new() { ["/"] = (HttpStatusCode.Forbidden, "denied") });
        var service = new StoreDetectService(new FakeHttpClientFactory(handler), new GlobalizationHelper());

        var result = (await service.DetectAsync(new StoreDetectRequest { Url = "https://loja.example" })).Result!;

        Assert.Equal((StoreDetectStatus.Blocked, StorePlatform.Manual), (result.Status, result.Platform));
        Assert.Equal(1, handler.Requests);   // nothing else is tried
    }

    [Fact]
    public async Task Detect_Unknown_WhenNothingAnswers()
    {
        var result = await DetectAsync(new() { ["/"] = (HttpStatusCode.OK, "<title>Site</title>") });

        Assert.Equal((StoreDetectStatus.Unknown, StorePlatform.Manual), (result.Status, result.Platform));
        Assert.NotEmpty(result.Evidence);
    }

    [Fact]
    public async Task Detect_RejectsAnInvalidAddress()
    {
        var service = new StoreDetectService(new FakeHttpClientFactory(new RoutingHandler([])), new GlobalizationHelper());

        Assert.Equal(ServiceApiResponseStatus.ValidationProblems, (await service.DetectAsync(new StoreDetectRequest { Url = "ftp://x" })).Status);
    }

    private StoreHealthService Health(IStoreAdapter adapter) =>
        new(_stores, _tasks, new StoreAdapterResolver([adapter]), new GlobalizationHelper(), TimeProvider.System);

    [Fact]
    public async Task Test_KeepsTheResultOnTheStore_AndCountsWhatTasksRecognise()
    {
        await _stores.SaveAsync(new Store { Name = "Loja", Platform = StorePlatform.Shopify, BaseUrl = "https://loja.example", UnmarkedLanguage = UnmarkedLanguagePolicy.English, Tags = ["untested", "PT"] }, null);
        await _tasks.SaveAsync(new StockTask { Id = "etb", Name = "ETB", SearchQuery = "etb", RequiredKeywords = ["elite trainer box"], MaxPrice = 70 });
        var adapter = new CannedAdapter(StorePlatform.Shopify,
            new StoreListing("Loja", "Pitch Black Elite Trainer Box", new Uri("https://loja.example/a"), 59.99m, true),
            new StoreListing("Loja", "Pitch Black Elite Trainer Box", new Uri("https://loja.example/b"), 99m, true),
            new StoreListing("Loja", "Card sleeves", new Uri("https://loja.example/c"), 5m, true));

        var result = (await Health(adapter).TestAsync(new StoreTestRequest { Name = "loja" })).Result!;

        Assert.Equal((StoreTestStatus.Ok, 3, 2, 1), (result.Status, result.Results, result.Relevant, result.Matches));
        Assert.Equal("pokemon", result.Query);
        var saved = (await _stores.GetAsync("Loja"))!;
        Assert.Equal(StoreTestStatus.Ok, saved.LastTest!.Status);
        Assert.Equal(["PT"], saved.Tags);   // tested with products: no longer "untested"
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, StoreTestStatus.Blocked)]
    [InlineData(HttpStatusCode.TooManyRequests, StoreTestStatus.RateLimited)]
    [InlineData(HttpStatusCode.InternalServerError, StoreTestStatus.Error)]
    public async Task Test_ReportsWhyItFailed(HttpStatusCode status, StoreTestStatus expected)
    {
        await _stores.SaveAsync(new Store { Name = "Loja", Platform = StorePlatform.Shopify, BaseUrl = "https://loja.example" }, null);
        var adapter = new CannedAdapter(StorePlatform.Shopify) { Failure = new HttpRequestException("x", null, status) };

        var result = (await Health(adapter).TestAsync(new StoreTestRequest { Name = "Loja" })).Result!;

        Assert.Equal(expected, result.Status);
        Assert.NotEmpty(result.Message);
    }

    [Fact]
    public async Task Test_UnknownStore_IsNotFound() =>
        Assert.Equal(ServiceApiResponseStatus.NotFound, (await Health(new CannedAdapter(StorePlatform.Shopify)).TestAsync(new StoreTestRequest { Name = "Nope" })).Status);

    [Fact]
    public async Task Cycles_TurnOffAStoreThatKeepsFailing_AndAWorkingCycleResetsTheCount()
    {
        await _stores.SaveAsync(new Store { Name = "Má", Platform = StorePlatform.Shopify, BaseUrl = "https://ma.example" }, null);
        await _stores.SaveAsync(new Store { Name = "Boa", Platform = StorePlatform.Shopify, BaseUrl = "https://boa.example" }, null);
        var health = Health(new CannedAdapter(StorePlatform.Shopify));

        await health.RecordCycleAsync(new Dictionary<string, bool> { ["Má"] = false, ["Boa"] = false }, 3);
        await health.RecordCycleAsync(new Dictionary<string, bool> { ["Má"] = false, ["Boa"] = true }, 3);
        var turnedOff = await health.RecordCycleAsync(new Dictionary<string, bool> { ["Má"] = false }, 3);

        Assert.Equal([("Má", 3)], turnedOff);
        var bad = (await _stores.GetAsync("Má"))!;
        Assert.False(bad.Enabled);
        Assert.NotNull(bad.AutoDisabledAt);
        Assert.Contains(StoreHealthService.FailingTag, bad.Tags);
        var good = (await _stores.GetAsync("Boa"))!;
        Assert.True(good.Enabled);
        Assert.Equal(0, good.ConsecutiveFailures);
    }

    [Fact]
    public async Task Cycles_NeverTurnOff_WhenTheLimitIsZero()
    {
        await _stores.SaveAsync(new Store { Name = "Má", Platform = StorePlatform.Shopify, BaseUrl = "https://ma.example" }, null);
        var health = Health(new CannedAdapter(StorePlatform.Shopify));

        for (var cycle = 0; cycle < 10; cycle++)
        {
            Assert.Empty(await health.RecordCycleAsync(new Dictionary<string, bool> { ["Má"] = false }, 0));
        }

        Assert.True((await _stores.GetAsync("Má"))!.Enabled);
    }

    [Fact]
    public async Task TurningOnAgain_ResetsTheCountAndTheTag()
    {
        await _stores.SaveAsync(new Store { Name = "Má", Platform = StorePlatform.Shopify, BaseUrl = "https://ma.example" }, null);
        var health = Health(new CannedAdapter(StorePlatform.Shopify));
        await health.RecordCycleAsync(new Dictionary<string, bool> { ["Má"] = false }, 1);
        var service = new StoreService(_stores, _tasks, new StoreAdapterResolver([]), new GlobalizationHelper());

        await service.EnableAsync(new StoreEnableRequest { Name = "Má", Enabled = true });

        var store = (await _stores.GetAsync("Má"))!;
        Assert.Equal((true, 0, (DateTimeOffset?)null), (store.Enabled, store.ConsecutiveFailures, store.AutoDisabledAt));
        Assert.DoesNotContain(StoreHealthService.FailingTag, store.Tags);
    }

    [Fact]
    public void ThrottledAndNotConfigured_DoNotCountAgainstTheStore()
    {
        Assert.False(StoreHealthService.IsStoreFailure(new TcgStockBot.WebApi.Extensions.Http.OutboundThrottledException("loja.example", TimeSpan.FromSeconds(5))));
        Assert.False(StoreHealthService.IsStoreFailure(new StoreIntegrationException("Amazon", StoreIntegrationProblem.NotConfigured, "x")));
        Assert.True(StoreHealthService.IsStoreFailure(new HttpRequestException("x", null, HttpStatusCode.Forbidden)));
    }

    /// <summary>Answers by path + query; anything else is a 404.</summary>
    private sealed class RoutingHandler(Dictionary<string, (HttpStatusCode Status, string Body)> routes) : HttpMessageHandler
    {
        public int Requests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            var (status, body) = routes.TryGetValue(request.RequestUri!.PathAndQuery, out var route) ? route : (HttpStatusCode.NotFound, "not found");
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "text/html"), RequestMessage = request });
        }
    }

    /// <summary>An adapter that returns fixed listings, or fails.</summary>
    private sealed class CannedAdapter(StorePlatform platform, params StoreListing[] listings) : IStoreAdapter
    {
        public Exception? Failure { get; init; }

        public StorePlatform Platform => platform;

        public Task<IReadOnlyList<StoreListing>> SearchAsync(Store store, string query, CancellationToken cancellationToken) =>
            Failure is null ? Task.FromResult<IReadOnlyList<StoreListing>>(listings) : Task.FromException<IReadOnlyList<StoreListing>>(Failure);
    }
}
