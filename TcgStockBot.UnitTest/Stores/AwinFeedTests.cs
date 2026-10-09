using System.IO.Compression;
using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using TcgStockBot.Library.Enums;
using TcgStockBot.UnitTest.Extensions;
using TcgStockBot.WebApi.Extensions.Http;
using TcgStockBot.WebApi.Features.Stores.Models.Entities;
using TcgStockBot.WebApi.Features.Stores.Services.Adapters;
using TcgStockBot.WebApi.Features.Stores.Services.Adapters.Awin;

namespace TcgStockBot.UnitTest.Stores;

/// <summary>
/// FNAC / Worten / El Corte Inglés providers: Awin product feed fixtures (Create-a-Feed CSV columns) and the
/// "not configured" limitation when no authorized feed exists. No request ever goes to the retailers' sites.
/// </summary>
public class AwinFeedTests
{
    private const string FeedUrl = "https://datafeed.api.productserve.com/datafeed/download/apikey/FEED-KEY-SECRET/fid/12345/format/csv/";

    private const string FeedCsv =
        "aw_product_id,merchant_product_id,merchant_name,product_name,search_price,currency,in_stock,stock_status,merchant_deep_link,aw_deep_link\r\n"
        + "1001,ECI-1,El Corte Inglés,\"Pokémon JCC Escarlata y Púrpura: Élite Trainer Box \"\"30th\"\" (EN)\",54.99,EUR,1,in stock,https://www.elcorteingles.es/juguetes/A1/,https://www.awin1.com/pclick.php?p=1001\r\n"
        + "1002,ECI-2,El Corte Inglés,Pokémon TCG Booster Bundle,29.95,EUR,0,out of stock,https://www.elcorteingles.es/juguetes/A2/,https://www.awin1.com/pclick.php?p=1002\r\n"
        + "1003,ECI-3,El Corte Inglés,\"Pokémon Elite Trainer Box, 30th Celebration\",59.99,EUR,,in_stock,https://www.elcorteingles.es/juguetes/A3/,\r\n"
        + "1004,ECI-4,El Corte Inglés,Lego City Camión,19.99,EUR,1,,https://www.elcorteingles.es/juguetes/A4/,\r\n"
        + "1005,ECI-5,El Corte Inglés,Pokémon sem link,9.99,EUR,1,,,\r\n";

    private static readonly Store EciStore = new() { Name = "El Corte Inglés", Platform = StorePlatform.ElCorteIngles, BaseUrl = "https://www.elcorteingles.es" };

    private static AwinFeedOptions Options(string? eciFeed = FeedUrl) => new()
    {
        Feeds = new(StringComparer.OrdinalIgnoreCase) { ["ElCorteIngles"] = eciFeed ?? string.Empty },
    };

    private static HttpResponseMessage Csv(string csv, bool gzip = false)
    {
        var bytes = Encoding.UTF8.GetBytes(csv);
        if (gzip)
        {
            using var buffer = new MemoryStream();
            using (var zip = new GZipStream(buffer, CompressionLevel.Fastest, leaveOpen: true))
            {
                zip.Write(bytes);
            }

            bytes = buffer.ToArray();
        }

        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };
    }

    private static (ElCorteInglesStoreAdapter Adapter, CountingFeedHandler Handler) CreateEci(AwinFeedOptions settings, Func<HttpResponseMessage> response)
    {
        var handler = new CountingFeedHandler(response);
        var monitor = new StaticOptionsMonitor<AwinFeedOptions>(settings);
        var catalog = new AwinFeedCatalog(monitor, new FakeHttpClientFactory(handler), TimeProvider.System, NullLogger<AwinFeedCatalog>.Instance);
        return (new ElCorteInglesStoreAdapter(monitor, catalog), handler);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Search_ReadsTheFeedAndNormalizesListings(bool gzip)
    {
        var (adapter, handler) = CreateEci(Options(), () => Csv(FeedCsv, gzip));

        var listings = await adapter.SearchAsync(EciStore, "elite trainer box", CancellationToken.None);

        Assert.Equal(FeedUrl, handler.LastUri!.AbsoluteUri);
        Assert.Collection(listings,
            first =>
            {
                Assert.Equal("Pokémon JCC Escarlata y Púrpura: Élite Trainer Box \"30th\" (EN)", first.Title);   // quotes + accents ("Élite" matches "elite")
                Assert.Equal(54.99m, first.Price);
                Assert.Equal("EUR", first.Currency);
                Assert.True(first.InStock);
                Assert.Equal("ECI-1", first.ProductId);
                Assert.Equal("El Corte Inglés", first.Seller);
                Assert.Equal("https://www.elcorteingles.es/juguetes/A1/", first.Url.AbsoluteUri);   // store link, not the affiliate redirect
            },
            second =>
            {
                Assert.Equal(59.99m, second.Price);   // comma inside a quoted title
                Assert.True(second.InStock);          // in_stock blank → stock_status "in_stock"
            });
    }

    [Fact]
    public async Task Search_FiltersLocally_AndKeepsOnlyKeepKeywordProducts()
    {
        var (adapter, _) = CreateEci(Options(), () => Csv(FeedCsv));

        var bundle = Assert.Single(await adapter.SearchAsync(EciStore, "booster bundle", CancellationToken.None));
        var lego = await adapter.SearchAsync(EciStore, "lego", CancellationToken.None);

        Assert.False(bundle.InStock);   // in_stock 0
        Assert.Empty(lego);             // not "pokemon": never kept in memory
    }

    [Fact]
    public async Task Feed_IsDownloadedOncePerRefreshInterval_ForEveryTask()
    {
        var (adapter, handler) = CreateEci(Options(), () => Csv(FeedCsv));

        await Task.WhenAll(
            adapter.SearchAsync(EciStore, "elite trainer box", CancellationToken.None),
            adapter.SearchAsync(EciStore, "booster bundle", CancellationToken.None),
            adapter.SearchAsync(EciStore, "pokemon", CancellationToken.None));

        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task FailedDownload_IsNotRetriedByEveryTask()
    {
        var (adapter, handler) = CreateEci(Options(), () => new HttpResponseMessage(HttpStatusCode.Forbidden));

        var first = await Assert.ThrowsAsync<StoreIntegrationException>(() => adapter.SearchAsync(EciStore, "etb", CancellationToken.None));
        await Assert.ThrowsAsync<StoreIntegrationException>(() => adapter.SearchAsync(EciStore, "booster bundle", CancellationToken.None));

        Assert.Equal(StoreIntegrationProblem.Unauthorized, first.Problem);
        Assert.Equal(1, handler.Calls);
        Assert.DoesNotContain("FEED-KEY-SECRET", first.Message);
    }

    [Fact]
    public async Task MissingColumns_AreReportedWithWhatToSelect()
    {
        var (adapter, _) = CreateEci(Options(), () => Csv("aw_product_id,product_name\r\n1,Pokémon ETB\r\n"));

        var exception = await Assert.ThrowsAsync<StoreIntegrationException>(() => adapter.SearchAsync(EciStore, "etb", CancellationToken.None));

        Assert.Equal(StoreIntegrationProblem.ApiError, exception.Problem);
        Assert.Contains("search_price", exception.Detail);
        Assert.Contains("merchant_deep_link", exception.Detail);
    }

    [Fact]
    public async Task PipeDelimitedFeed_IsSupported()
    {
        var (adapter, _) = CreateEci(Options(), () => Csv("product_name|search_price|in_stock|merchant_deep_link\r\nPokémon ETB|49.99|1|https://www.elcorteingles.es/p/1/\r\n"));

        var listing = Assert.Single(await adapter.SearchAsync(EciStore, "etb", CancellationToken.None));

        Assert.Equal(49.99m, listing.Price);
    }

    [Fact]
    public async Task NonHttpsFeedUrl_IsRejectedWithoutDownloading()
    {
        var (adapter, handler) = CreateEci(Options("http://datafeed.example/feed.csv"), () => Csv(FeedCsv));

        var exception = await Assert.ThrowsAsync<StoreIntegrationException>(() => adapter.SearchAsync(EciStore, "etb", CancellationToken.None));

        Assert.Equal(StoreIntegrationProblem.NotConfigured, exception.Problem);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task EveryRetailer_WithoutFeed_ReportsTheLimitationAndContactsNobody()
    {
        var handler = new CountingFeedHandler(() => Csv(FeedCsv));
        var monitor = new StaticOptionsMonitor<AwinFeedOptions>(new AwinFeedOptions());
        var catalog = new AwinFeedCatalog(monitor, new FakeHttpClientFactory(handler), TimeProvider.System, NullLogger<AwinFeedCatalog>.Instance);
        IStoreAdapter[] adapters = [new FnacStoreAdapter(monitor, catalog), new WortenStoreAdapter(monitor, catalog), new ElCorteInglesStoreAdapter(monitor, catalog)];

        foreach (var adapter in adapters)
        {
            var store = new Store { Name = adapter.Platform.ToString(), Platform = adapter.Platform, BaseUrl = "https://example.pt" };
            var exception = await Assert.ThrowsAsync<StoreIntegrationException>(() => adapter.SearchAsync(store, "etb", CancellationToken.None));

            Assert.Equal(StoreIntegrationProblem.NotConfigured, exception.Problem);
            Assert.Contains($"AwinFeeds__Feeds__{adapter.Platform}", exception.Detail);
        }

        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task OutboundLog_ShowsTheFeedKeyNotTheSecretPath()
    {
        var logger = new ListLogger();
        var outbound = new OutboundHttpOptions { DefaultRateLimitSeconds = 0 };
        var traffic = new OutboundTrafficHandler(
            new DomainThrottle(new StaticOptionsMonitor<OutboundHttpOptions>(outbound), TimeProvider.System),
            new TypedLogger<OutboundTrafficHandler>(logger))
        {
            InnerHandler = new CountingFeedHandler(() => Csv(FeedCsv)),
        };
        var monitor = new StaticOptionsMonitor<AwinFeedOptions>(Options());
        var catalog = new AwinFeedCatalog(monitor, new FakeHttpClientFactory(traffic), TimeProvider.System, NullLogger<AwinFeedCatalog>.Instance);

        await new ElCorteInglesStoreAdapter(monitor, catalog).SearchAsync(EciStore, "etb", CancellationToken.None);

        var logged = Assert.Single(logger.Entries).Message;
        Assert.Contains("datafeed.api.productserve.com/… (Awin feed ElCorteIngles)", logged);
        Assert.DoesNotContain("FEED-KEY-SECRET", logged);
        Assert.DoesNotContain("apikey", logged);
    }

    [Theory]
    [InlineData("1", null, null, true)]
    [InlineData("0", "in stock", null, false)]
    [InlineData("yes", null, null, true)]         // Awin: any other text = in stock
    [InlineData("", null, "3", true)]
    [InlineData("", null, "0", false)]
    [InlineData("", "Out of stock", null, false)]
    [InlineData("", "preorder", null, true)]
    [InlineData("", "Agotado", null, false)]
    [InlineData("", "", null, false)]             // blank = no stock information
    public void InStock_FollowsAwinColumns(string inStock, string? status, string? quantity, bool expected) =>
        Assert.Equal(expected, AwinFeedCatalog.ParseInStock(inStock, status, quantity));

    /// <summary>Answers every request with a fresh response and counts calls.</summary>
    private sealed class CountingFeedHandler(Func<HttpResponseMessage> response) : HttpMessageHandler
    {
        private int _calls;

        public int Calls => _calls;

        public Uri? LastUri { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _calls);
            LastUri = request.RequestUri;
            await Task.Delay(20, cancellationToken);   // lets concurrent searches overlap
            return response();
        }
    }
}
