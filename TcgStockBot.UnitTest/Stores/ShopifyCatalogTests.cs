using System.Net;
using System.Text;
using System.Text.Json;
using TcgStockBot.Library.Enums;
using TcgStockBot.WebApi.Features.Stores.Models.Entities;
using TcgStockBot.WebApi.Features.Stores.Services.Adapters;

namespace TcgStockBot.UnitTest.Stores;

/// <summary>
/// Shopify catalog mode: /collections/…/products.json read page by page, merged, cached for the cycle and filtered
/// locally (no 10-product limit). Without catalog paths the predictive search is still used.
/// </summary>
public class ShopifyCatalogTests
{
    private static Store Store(string paths) => new()
    {
        Name = "Versus", Platform = StorePlatform.Shopify, BaseUrl = "https://versus.example", ShopifyCatalogPaths = paths,
    };

    private static string Product(long id, string title, string handle, string tags, params (string Price, bool Available)[] variants) =>
        JsonSerializer.Serialize(new
        {
            id, title, handle, vendor = "Pokémon", product_type = "TCG", tags = tags.Split(',', StringSplitOptions.RemoveEmptyEntries),
            variants = variants.Select(static variant => new { price = variant.Price, available = variant.Available }),
        });

    private static string Page(params string[] products) => $"{{\"products\":[{string.Join(',', products)}]}}";

    [Theory]
    [InlineData("", 0)]
    [InlineData("/collections/pokemon-tcg-1", 1)]
    [InlineData("/collections/pokemon-tcg-1, /collections/riftbound-league-of-legends-tcg", 2)]
    [InlineData("collections/pokemon/, /collections/pokemon", 1)]
    [InlineData("/products.json", 1)]
    public void CatalogPaths_AreNormalized(string configured, int expected)
    {
        var paths = ShopifyStoreAdapter.CatalogPaths(configured);

        Assert.Equal(expected, paths.Count);
        Assert.All(paths, static path => Assert.EndsWith(".json", path));
    }

    [Fact]
    public async Task Catalog_ReadsEveryPage_MergesCollections_AndFiltersLocally()
    {
        var pageOne = Page(Enumerable.Range(1, ShopifyStoreAdapter.PageSize)
            .Select(index => Product(index, $"Filler product {index}", $"filler-{index}", "", ("1.00", true))).ToArray());
        var pageTwo = Page(
            Product(900, "Pokémon TCG: Mega Evolution 5 - Pitch Black Elite Trainer Box", "pitch-black-etb", "Pokémon,Novidade", ("89.90", false), ("99.90", true)),
            Product(901, "Pitch Black Booster Display (36 Booster)", "pitch-black-display", "", ("249.90", false)));
        var riftbound = Page(
            Product(900, "Pokémon TCG: Mega Evolution 5 - Pitch Black Elite Trainer Box", "pitch-black-etb", "", ("99.90", true)),   // also in this collection
            Product(950, "Riftbound Radiance Booster Box", "radiance-box", "Riftbound", ("130.00", true)));

        var handler = new RoutingHandler(uri => uri.AbsolutePath switch
        {
            "/collections/pokemon-tcg-1/products.json" when uri.Query.Contains("page=1") => pageOne,
            "/collections/pokemon-tcg-1/products.json" when uri.Query.Contains("page=2") => pageTwo,
            "/collections/riftbound/products.json" => riftbound,
            _ => Page(),
        });
        var adapter = new ShopifyStoreAdapter(new FakeHttpClientFactory(handler));
        var store = Store("/collections/pokemon-tcg-1, /collections/riftbound");

        var listings = await adapter.SearchAsync(store, "pitch black", CancellationToken.None);

        Assert.Equal(3, handler.Requests.Count);   // 2 pages of the first collection (page 2 was short), 1 of the second
        Assert.Collection(listings.OrderBy(static listing => listing.Title),
            display =>
            {
                Assert.False(display.InStock);
                Assert.Equal(249.90m, display.Price);
            },
            etb =>
            {
                Assert.Equal(new Uri("https://versus.example/products/pitch-black-etb"), etb.Url);
                Assert.True(etb.InStock);
                Assert.Equal(99.90m, etb.Price);   // the cheapest AVAILABLE variant, not the sold-out 89,90
                Assert.Equal("900", etb.ProductId);
            });
    }

    [Fact]
    public async Task Catalog_IsReadOncePerCycle_ForEveryTask()
    {
        var handler = new RoutingHandler(_ => Page(
            Product(1, "Perfect Order Booster Box", "po-box", "Pokémon", ("189.99", true)),
            Product(2, "Perfect Order Elite Trainer Box", "po-etb", "Pokémon", ("69.90", true))));
        var adapter = new ShopifyStoreAdapter(new FakeHttpClientFactory(handler));
        var store = Store("/collections/pokemon");

        var boxes = await adapter.SearchAsync(store, "booster box", CancellationToken.None);
        var etbs = await adapter.SearchAsync(store, "elite trainer box", CancellationToken.None);
        var byTag = await adapter.SearchAsync(store, "pokemon perfect order", CancellationToken.None);   // "pokemon" only in the tags

        Assert.Single(handler.Requests);
        Assert.Single(boxes);
        Assert.Single(etbs);
        Assert.Equal(2, byTag.Count);
    }

    [Fact]
    public async Task Catalog_WordsMatchWholeWordsOnly()
    {
        var handler = new RoutingHandler(_ => Page(Product(1, "Elite Trainer Boxes Storage", "storage", "", ("20.00", true))));
        var adapter = new ShopifyStoreAdapter(new FakeHttpClientFactory(handler));

        Assert.Empty(await adapter.SearchAsync(Store("/collections/pokemon"), "elite trainer box", CancellationToken.None));
    }

    [Fact]
    public async Task Catalog_FailureIsNotRetriedByEveryTask()
    {
        var handler = new RoutingHandler(_ => null);   // 403 for everything
        var adapter = new ShopifyStoreAdapter(new FakeHttpClientFactory(handler));
        var store = Store("/collections/pokemon");

        var first = await Assert.ThrowsAsync<HttpRequestException>(() => adapter.SearchAsync(store, "etb", CancellationToken.None));
        await Assert.ThrowsAsync<HttpRequestException>(() => adapter.SearchAsync(store, "booster box", CancellationToken.None));

        Assert.Equal(HttpStatusCode.Forbidden, first.StatusCode);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task NoCatalogPaths_KeepsThePredictiveSearch()
    {
        var handler = new RoutingHandler(_ => """{"resources":{"results":{"products":[]}}}""");
        var adapter = new ShopifyStoreAdapter(new FakeHttpClientFactory(handler));

        await adapter.SearchAsync(Store(""), "pitch black", CancellationToken.None);

        Assert.Equal("/search/suggest.json", Assert.Single(handler.Requests).AbsolutePath);
    }

    /// <summary>Answers by URL (null = 403) and records the requests.</summary>
    private sealed class RoutingHandler(Func<Uri, string?> body) : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            lock (Requests)
            {
                Requests.Add(request.RequestUri!);
            }

            var content = body(request.RequestUri!);
            return Task.FromResult(content is null
                ? new HttpResponseMessage(HttpStatusCode.Forbidden)
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(content, Encoding.UTF8, "application/json") });
        }
    }
}
