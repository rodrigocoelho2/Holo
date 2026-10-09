using System.Text;
using System.Text.Json;
using TcgStockBot.Library.Enums;
using TcgStockBot.WebApi.Features.Stores.Models.Entities;
using TcgStockBot.WebApi.Features.Stores.Services.Adapters;
using TcgStockBot.WebApi.Features.Stores.Services.Adapters.Amazon;
using TcgStockBot.WebApi.Features.Stores.Services.Adapters.Awin;

namespace TcgStockBot.UnitTest.Stores;

/// <summary>
/// Every adapter brings the product photo along (ImageUrl), always as an absolute http(s) URL or null.
/// </summary>
public class StoreImageTests
{
    [Theory]
    [InlineData("https://cdn.shop.example/etb.jpg", null, "https://cdn.shop.example/etb.jpg")]
    [InlineData("//cdn.shopify.com/s/files/etb.jpg", null, "https://cdn.shopify.com/s/files/etb.jpg")]
    [InlineData("/media/etb.jpg", "https://html.example/pesquisa?q=etb", "https://html.example/media/etb.jpg")]
    [InlineData("media/etb.jpg", "https://html.example/loja/pesquisa", "https://html.example/loja/media/etb.jpg")]
    [InlineData("/media/etb.jpg", null, null)]
    [InlineData("data:image/gif;base64,R0lGODlhAQABAAAAACw=", "https://html.example/", null)]
    [InlineData("javascript:alert(1)", "https://html.example/", null)]
    [InlineData("   ", null, null)]
    [InlineData(null, null, null)]
    public void Normalize_ReturnsAnAbsoluteHttpUrlOrNull(string? raw, string? baseUrl, string? expected) =>
        Assert.Equal(expected, StoreImageUrl.Normalize(raw, baseUrl is null ? null : new Uri(baseUrl)));

    [Fact]
    public async Task Shopify_ReadsFeaturedImage_ThenImage()
    {
        const string body = """
            {"resources":{"results":{"products":[
              {"title":"ETB","url":"/products/etb","price":"54.99","available":true,"image":"//cdn.shopify.com/old.jpg","featured_image":{"url":"https://cdn.shopify.com/etb.jpg","alt":"ETB"}},
              {"title":"Bundle","url":"/products/bundle","price":"32.90","available":true,"image":"//cdn.shopify.com/bundle.jpg"},
              {"title":"Tin","url":"/products/tin","price":"12.00","available":true}
            ]}}}
            """;
        var adapter = new ShopifyStoreAdapter(new FakeHttpClientFactory(new FakeHttpMessageHandler(body)));
        var store = new Store { Name = "Shop", Platform = StorePlatform.Shopify, BaseUrl = "https://shop.example" };

        var listings = await adapter.SearchAsync(store, "etb", CancellationToken.None);

        Assert.Equal("https://cdn.shopify.com/etb.jpg", listings[0].ImageUrl);
        Assert.Equal("https://cdn.shopify.com/bundle.jpg", listings[1].ImageUrl);
        Assert.Null(listings[2].ImageUrl);
    }

    [Fact]
    public async Task WooCommerce_ReadsTheFirstImage()
    {
        const string body = """
            [{"name":"ETB","permalink":"https://woo.example/produto/etb/","is_in_stock":true,"is_purchasable":true,
              "prices":{"price":"5499","currency_minor_unit":2},
              "images":[{"id":1,"src":"https://woo.example/wp-content/uploads/etb.jpg","thumbnail":"https://woo.example/wp-content/uploads/etb-300x300.jpg"}]},
             {"name":"Bundle","permalink":"https://woo.example/produto/bundle/","is_in_stock":true,"is_purchasable":true,
              "prices":{"price":"3290","currency_minor_unit":2},"images":[]}]
            """;
        var adapter = new WooCommerceStoreAdapter(new FakeHttpClientFactory(new FakeHttpMessageHandler(body)));
        var store = new Store { Name = "Woo", Platform = StorePlatform.WooCommerce, BaseUrl = "https://woo.example", WooCommerceProductsPath = "/wp-json/wc/store/products" };

        var listings = await adapter.SearchAsync(store, "etb", CancellationToken.None);

        Assert.Equal("https://woo.example/wp-content/uploads/etb.jpg", listings[0].ImageUrl);
        Assert.Null(listings[1].ImageUrl);
    }

    [Fact]
    public async Task Html_PrefersLazyLoadedSource_AndResolvesRelativeUrls()
    {
        const string body = """
            <html><body>
              <div class="product"><img src="data:image/gif;base64,R0lGOD" data-src="/media/etb.jpg"><a class="name" href="/p/etb">ETB</a></div>
              <div class="product"><img src="https://cdn.html.example/bundle.jpg"><a class="name" href="/p/bundle">Bundle</a></div>
              <div class="product"><a class="name" href="/p/tin">Tin</a></div>
            </body></html>
            """;
        var adapter = new HtmlStoreAdapter(new FakeHttpClientFactory(new FakeHttpMessageHandler(body, "text/html")));
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
                LinkSelector = "a.name",
            },
        };

        var listings = await adapter.SearchAsync(store, "etb", CancellationToken.None);

        Assert.Equal("https://html.example/media/etb.jpg", listings[0].ImageUrl);
        Assert.Equal("https://cdn.html.example/bundle.jpg", listings[1].ImageUrl);
        Assert.Null(listings[2].ImageUrl);
    }

    [Fact]
    public void Amazon_ReadsThePrimaryLargeImage()
    {
        const string json = """
            {"searchResult":{"items":[
              {"asin":"B0TEST0001","detailPageURL":"https://www.amazon.es/dp/B0TEST0001",
               "itemInfo":{"title":{"displayValue":"Pokémon ETB"}},
               "images":{"primary":{"large":{"url":"https://m.media-amazon.com/images/I/etb.jpg","height":500,"width":500}}}},
              {"asin":"B0TEST0002","detailPageURL":"https://www.amazon.es/dp/B0TEST0002",
               "itemInfo":{"title":{"displayValue":"Pokémon Bundle"}}}
            ]}}
            """;
        var store = new Store { Name = "Amazon", Platform = StorePlatform.Amazon, BaseUrl = "https://www.amazon.es" };
        using var document = JsonDocument.Parse(json);

        var listings = AmazonCreatorsStoreAdapter.Map(store, document.RootElement);

        Assert.Equal("https://m.media-amazon.com/images/I/etb.jpg", listings[0].ImageUrl);
        Assert.Null(listings[1].ImageUrl);
    }

    [Fact]
    public void Awin_ReadsTheMerchantImage_ThenAwinsCopy()
    {
        const string csv =
            "aw_product_id,product_name,search_price,currency,in_stock,merchant_deep_link,merchant_image_url,aw_image_url\r\n"
            + "1,Pokémon ETB,54.99,EUR,1,https://shop.example/etb,https://shop.example/etb.jpg,https://images2.productserve.com/etb.jpg\r\n"
            + "2,Pokémon Bundle,29.95,EUR,1,https://shop.example/bundle,,https://images2.productserve.com/bundle.jpg\r\n"
            + "3,Pokémon Tin,9.99,EUR,1,https://shop.example/tin,,\r\n";
        using var content = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var (products, _) = AwinFeedCatalog.Parse("Test", content, new AwinFeedOptions(), CancellationToken.None);

        Assert.Equal("https://shop.example/etb.jpg", products[0].ImageUrl);
        Assert.Equal("https://images2.productserve.com/bundle.jpg", products[1].ImageUrl);
        Assert.Null(products[2].ImageUrl);
    }
}
