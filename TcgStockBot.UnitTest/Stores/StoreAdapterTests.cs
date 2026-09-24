using System.Net;
using TcgStockBot.Library.Enums;
using TcgStockBot.WebApi.Features.Stores.Models.Entities;
using TcgStockBot.WebApi.Features.Stores.Services.Adapters;

namespace TcgStockBot.UnitTest.Stores;

public class StoreAdapterTests
{
    [Fact]
    public async Task Shopify_ParsesPredictiveSearch()
    {
        const string body = """
            {"resources":{"results":{"products":[
              {"title":"Pok&eacute;mon Elite Trainer Box (EN)","url":"/products/etb-en?_pos=1&_sid=abc&_ss=r","price":"54.99","available":true},
              {"title":"Pokémon Elite Trainer Box (PT)","url":"/products/etb-pt?_pos=2","price":"49.99","available":false}
            ]}}}
            """;
        var handler = new FakeHttpMessageHandler(body);
        var adapter = new ShopifyStoreAdapter(new FakeHttpClientFactory(handler));
        var store = new Store { Name = "Shop", Platform = StorePlatform.Shopify, BaseUrl = "https://shop.example" };

        var listings = await adapter.SearchAsync(store, "elite trainer box", CancellationToken.None);

        Assert.StartsWith("https://shop.example/search/suggest.json?q=elite%20trainer%20box", handler.LastRequestUri!.AbsoluteUri);
        Assert.Collection(listings,
            first =>
            {
                Assert.Equal("Pokémon Elite Trainer Box (EN)", first.Title);
                Assert.Equal(new Uri("https://shop.example/products/etb-en"), first.Url);
                Assert.Equal(54.99m, first.Price);
                Assert.True(first.InStock);
            },
            second => Assert.False(second.InStock));
    }

    [Fact]
    public async Task WooCommerce_UsesTheConfiguredPath()
    {
        const string body = """
            [{"name":"Elite Trainer Box &#8211; English","permalink":"https://woo.example/produto/etb/",
              "is_in_stock":true,"is_purchasable":true,
              "prices":{"price":"5499","currency_code":"EUR","currency_minor_unit":2}}]
            """;
        var handler = new FakeHttpMessageHandler(body);
        var adapter = new WooCommerceStoreAdapter(new FakeHttpClientFactory(handler));
        var store = new Store { Name = "Woo", Platform = StorePlatform.WooCommerce, BaseUrl = "https://woo.example", WooCommerceProductsPath = "/wp-json/wc/store/products" };

        var listings = await adapter.SearchAsync(store, "etb", CancellationToken.None);

        Assert.Equal("https://woo.example/wp-json/wc/store/products?search=etb&per_page=20", handler.LastRequestUri!.AbsoluteUri);
        Assert.Equal("Elite Trainer Box – English", listings[0].Title);
        Assert.Equal(54.99m, listings[0].Price);
        Assert.True(listings[0].InStock);
    }

    [Fact]
    public async Task Html_ReadsCardsWithConfiguredSelectors()
    {
        const string body = """
            <html><body>
              <div class="product"><a class="name" href="/p/etb-en">Elite Trainer Box (EN)</a><span class="price">54,99 €</span><button class="add">Comprar</button></div>
              <div class="product"><a class="name" href="/p/etb-pt">Elite Trainer Box (PT)</a><span class="price">49,99 €</span><span class="sold-out">Esgotado</span></div>
            </body></html>
            """;
        var handler = new FakeHttpMessageHandler(body, "text/html");
        var adapter = new HtmlStoreAdapter(new FakeHttpClientFactory(handler));
        var store = new Store
        {
            Name = "Html",
            Platform = StorePlatform.Html,
            BaseUrl = "https://html.example",
            Html = new StoreHtmlSelector
            {
                SearchUrlTemplate = "https://html.example/pesquisa?q={query}",
                ProductSelector = "div.product",
                TitleSelector = "a.name",
                PriceSelector = ".price",
                LinkSelector = "a.name",
                OutOfStockSelector = ".sold-out",
            },
        };

        var listings = await adapter.SearchAsync(store, "elite trainer box", CancellationToken.None);

        Assert.Equal("https://html.example/pesquisa?q=elite%20trainer%20box", handler.LastRequestUri!.AbsoluteUri);
        Assert.Equal(new Uri("https://html.example/p/etb-en"), listings[0].Url);
        Assert.True(listings[0].InStock);
        Assert.False(listings[1].InStock);
    }

    [Fact]
    public async Task Shopify_HttpError_Throws()
    {
        var adapter = new ShopifyStoreAdapter(new FakeHttpClientFactory(new FakeHttpMessageHandler("blocked", "text/html", HttpStatusCode.Forbidden)));
        var store = new Store { Name = "Shop", Platform = StorePlatform.Shopify, BaseUrl = "https://shop.example" };

        await Assert.ThrowsAsync<HttpRequestException>(() => adapter.SearchAsync(store, "etb", CancellationToken.None));
    }
}
