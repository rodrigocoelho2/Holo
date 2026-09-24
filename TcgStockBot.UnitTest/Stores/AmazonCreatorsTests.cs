using System.Net;
using System.Text;
using System.Text.Json;
using TcgStockBot.Library.Enums;
using TcgStockBot.WebApi.Features.Stores.Models.Entities;
using TcgStockBot.WebApi.Features.Stores.Services.Adapters;
using TcgStockBot.WebApi.Features.Stores.Services.Adapters.Amazon;

namespace TcgStockBot.UnitTest.Stores;

/// <summary>
/// Amazon Creators API provider, against fixtures shaped like the official documentation (no real credentials).
/// </summary>
public class AmazonCreatorsTests
{
    private const string TokenJson = """{"access_token":"Atc|test-token","scope":"creatorsapi::default","token_type":"bearer","expires_in":3600}""";

    private const string SearchJson = """
        {"searchResult":{"totalResultCount":2,"items":[
          {"asin":"B0TEST0001","detailPageURL":"https://www.amazon.es/dp/B0TEST0001?tag=mytag-21",
           "itemInfo":{"title":{"displayValue":"Pokémon TCG: 30th Celebration Elite Trainer Box (EN)","label":"Title","locale":"es_ES"}},
           "offersV2":{"listings":[
             {"isBuyBoxWinner":false,"price":{"money":{"amount":95.00,"currency":"EUR","displayAmount":"95,00 €"}},"availability":{"type":"IN_STOCK"},"merchantInfo":{"name":"Revendedor X"}},
             {"isBuyBoxWinner":true,"price":{"money":{"amount":79.99,"currency":"EUR","displayAmount":"79,99 €"}},"availability":{"type":"IN_STOCK","message":"En stock"},"merchantInfo":{"name":"Amazon.es","id":"A1AT7YVPFBWXBL"}}]}},
          {"asin":"B0TEST0002","detailPageURL":"https://www.amazon.es/dp/B0TEST0002",
           "itemInfo":{"title":{"displayValue":"Pokémon TCG: 30th Celebration Booster Bundle"}},
           "offersV2":{"listings":[{"price":{"money":{"amount":34.99,"currency":"EUR"}},"availability":{"type":"OUT_OF_STOCK"}}]}}]}}
        """;

    private const string GetItemsJson = """
        {"itemsResult":{"items":[
          {"asin":"B0TEST0003","detailPageURL":"https://www.amazon.es/dp/B0TEST0003",
           "itemInfo":{"title":{"displayValue":"Pokémon TCG: Ultra-Premium Collection"}},
           "offersV2":{"listings":[{"isBuyBoxWinner":true,"price":{"money":{"amount":129.99,"currency":"EUR"}},"availability":{"type":"PREORDER"},"merchantInfo":{"name":"Amazon.es"}}]}}]}}
        """;

    private static readonly Store AmazonStore = new() { Name = "Amazon", Platform = StorePlatform.Amazon, BaseUrl = "https://www.amazon.es" };

    private static AmazonCreatorsOptions ValidOptions() => new()
    {
        CredentialId = "amzn1.application-oa2-client.test",
        CredentialSecret = "super-secret-value",
        CredentialVersion = "3.2",
        PartnerTag = "mytag-21",
    };

    private static (AmazonCreatorsStoreAdapter Adapter, RoutingHandler Handler, AmazonCreatorsTokenProvider Tokens) Create(
        AmazonCreatorsOptions settings, Func<HttpRequestMessage, string, HttpResponseMessage> api)
    {
        var handler = new RoutingHandler(api);
        var factory = new FakeHttpClientFactory(handler);
        var monitor = new StaticOptionsMonitor<AmazonCreatorsOptions>(settings);
        var tokens = new AmazonCreatorsTokenProvider(monitor, factory, TimeProvider.System);
        return (new AmazonCreatorsStoreAdapter(monitor, tokens, factory), handler, tokens);
    }

    private static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task Search_AuthenticatesAndMapsBuyBoxOffer()
    {
        var (adapter, handler, _) = Create(ValidOptions(), (_, _) => Json(SearchJson));

        var listings = await adapter.SearchAsync(AmazonStore, "30th celebration elite trainer box", CancellationToken.None);

        // Token request: EU (3.2) endpoint, JSON body with the documented fields and scope.
        var tokenCall = Assert.Single(handler.Calls, static call => call.Uri.Host == "api.amazon.co.uk");
        Assert.Equal("/auth/o2/token", tokenCall.Uri.AbsolutePath);
        using (var tokenBody = JsonDocument.Parse(tokenCall.Body))
        {
            Assert.Equal("client_credentials", tokenBody.RootElement.GetProperty("grant_type").GetString());
            Assert.Equal("creatorsapi::default", tokenBody.RootElement.GetProperty("scope").GetString());
        }

        // API request: bearer token, marketplace header, partner tag and resources.
        var apiCall = Assert.Single(handler.Calls, static call => call.Uri.Host == "creatorsapi.amazon");
        Assert.Equal("/catalog/v1/searchItems", apiCall.Uri.AbsolutePath);
        Assert.Equal("Bearer Atc|test-token", apiCall.Authorization);
        Assert.Equal("www.amazon.es", apiCall.Marketplace);
        using (var apiBody = JsonDocument.Parse(apiCall.Body))
        {
            Assert.Equal("mytag-21", apiBody.RootElement.GetProperty("partnerTag").GetString());
            Assert.Contains("offersV2.listings.price", apiBody.RootElement.GetProperty("resources").EnumerateArray().Select(static value => value.GetString()));
        }

        Assert.Collection(listings,
            first =>
            {
                Assert.Equal("B0TEST0001", first.ProductId);
                Assert.Equal(79.99m, first.Price);                 // buy-box winner, not the first listing
                Assert.Equal("EUR", first.Currency);
                Assert.True(first.InStock);
                Assert.Equal("Amazon.es", first.Seller);
                Assert.Equal("https://www.amazon.es/dp/B0TEST0001?tag=mytag-21", first.Url.AbsoluteUri);
            },
            second =>
            {
                Assert.Equal(34.99m, second.Price);
                Assert.False(second.InStock);                      // OUT_OF_STOCK
            });
    }

    [Fact]
    public async Task Asin_UsesGetItems_AndPreorderCountsAsAvailable()
    {
        var (adapter, handler, _) = Create(ValidOptions(), (_, _) => Json(GetItemsJson));

        var listing = Assert.Single(await adapter.SearchAsync(AmazonStore, "asin:b0test0003", CancellationToken.None));

        var apiCall = Assert.Single(handler.Calls, static call => call.Uri.Host == "creatorsapi.amazon");
        Assert.Equal("/catalog/v1/getItems", apiCall.Uri.AbsolutePath);
        using var body = JsonDocument.Parse(apiCall.Body);
        Assert.Equal("B0TEST0003", body.RootElement.GetProperty("itemIds")[0].GetString());
        Assert.Equal(129.99m, listing.Price);
        Assert.True(listing.InStock);
    }

    [Fact]
    public void GetItems_AcceptsTheDocumentationResultKeyToo()
    {
        using var document = JsonDocument.Parse(GetItemsJson.Replace("itemsResult", "itemResults"));

        Assert.Single(AmazonCreatorsStoreAdapter.Map(AmazonStore, document.RootElement));
    }

    [Fact]
    public async Task Token_IsCachedAcrossCalls()
    {
        var (adapter, handler, _) = Create(ValidOptions(), (_, _) => Json(SearchJson));

        await adapter.SearchAsync(AmazonStore, "etb", CancellationToken.None);
        await adapter.SearchAsync(AmazonStore, "etb", CancellationToken.None);
        await adapter.SearchAsync(AmazonStore, "booster bundle", CancellationToken.None);

        Assert.Equal(1, handler.Calls.Count(static call => call.Uri.Host == "api.amazon.co.uk"));
        Assert.Equal(3, handler.Calls.Count(static call => call.Uri.Host == "creatorsapi.amazon"));
    }

    [Fact]
    public async Task Unauthorized_RefreshesTokenOnce_ThenReportsCredentials()
    {
        var (adapter, handler, _) = Create(ValidOptions(), (_, _) => Json("""{"type":"UnauthorizedException","message":"Invalid token"}""", HttpStatusCode.Unauthorized));

        var exception = await Assert.ThrowsAsync<StoreIntegrationException>(() => adapter.SearchAsync(AmazonStore, "etb", CancellationToken.None));

        Assert.Equal(StoreIntegrationProblem.Unauthorized, exception.Problem);
        Assert.Equal(2, handler.Calls.Count(static call => call.Uri.Host == "api.amazon.co.uk"));   // refreshed once
        Assert.Equal(2, handler.Calls.Count(static call => call.Uri.Host == "creatorsapi.amazon"));
    }

    [Fact]
    public async Task ApiValidationError_IsReportedWithTypeAndReason()
    {
        var (adapter, _, _) = Create(ValidOptions(), (_, _) =>
            Json("""{"type":"ValidationException","message":"The partner tag is invalid.","reason":"InvalidPartnerTag"}""", HttpStatusCode.BadRequest));

        var exception = await Assert.ThrowsAsync<StoreIntegrationException>(() => adapter.SearchAsync(AmazonStore, "etb", CancellationToken.None));

        Assert.Equal(StoreIntegrationProblem.ApiError, exception.Problem);
        Assert.Contains("InvalidPartnerTag", exception.Detail);
    }

    [Fact]
    public async Task Throttled_Surfaces429()
    {
        var (adapter, _, _) = Create(ValidOptions(), (_, _) => Json("""{"type":"ThrottleException","message":"Rate exceeded"}""", HttpStatusCode.TooManyRequests));

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => adapter.SearchAsync(AmazonStore, "etb", CancellationToken.None));

        Assert.Equal(HttpStatusCode.TooManyRequests, exception.StatusCode);
    }

    [Fact]
    public async Task MissingCredentials_AreReportedWithoutCallingAmazon()
    {
        var (adapter, handler, _) = Create(new AmazonCreatorsOptions(), (_, _) => Json(SearchJson));

        var exception = await Assert.ThrowsAsync<StoreIntegrationException>(() => adapter.SearchAsync(AmazonStore, "etb", CancellationToken.None));

        Assert.Equal(StoreIntegrationProblem.NotConfigured, exception.Problem);
        Assert.Contains("Amazon__CredentialId", exception.Detail);
        Assert.Empty(handler.Calls);
    }

    [Fact]
    public async Task RejectedCredentials_AtTokenEndpoint_NeverEchoTheSecret()
    {
        var handler = new RoutingHandler((_, _) => Json(SearchJson), tokenStatus: HttpStatusCode.BadRequest);
        var factory = new FakeHttpClientFactory(handler);
        var monitor = new StaticOptionsMonitor<AmazonCreatorsOptions>(ValidOptions());
        var adapter = new AmazonCreatorsStoreAdapter(monitor, new AmazonCreatorsTokenProvider(monitor, factory, TimeProvider.System), factory);

        var exception = await Assert.ThrowsAsync<StoreIntegrationException>(() => adapter.SearchAsync(AmazonStore, "etb", CancellationToken.None));

        Assert.Equal(StoreIntegrationProblem.Unauthorized, exception.Problem);
        Assert.DoesNotContain("super-secret-value", exception.Message);
    }

    [Theory]
    [InlineData("B0DZ123ABC", "B0DZ123ABC")]
    [InlineData("asin: b0dz123abc", "B0DZ123ABC")]
    [InlineData("elite trainer box", null)]
    [InlineData("B0DZ123", null)]
    public void ParseAsin(string query, string? expected) =>
        Assert.Equal(expected, AmazonCreatorsStoreAdapter.ParseAsin(query));

    /// <summary>Answers the token endpoint itself and forwards API calls to a delegate; records every call.</summary>
    private sealed class RoutingHandler(Func<HttpRequestMessage, string, HttpResponseMessage> api, HttpStatusCode tokenStatus = HttpStatusCode.OK)
        : HttpMessageHandler
    {
        public List<(Uri Uri, string Body, string? Authorization, string? Marketplace)> Calls { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            Calls.Add((request.RequestUri!, body, request.Headers.Authorization?.ToString(),
                request.Headers.TryGetValues("x-marketplace", out var marketplace) ? marketplace.Single() : null));

            return request.RequestUri!.Host == "api.amazon.co.uk"
                ? Json(tokenStatus == HttpStatusCode.OK ? TokenJson : """{"error":"invalid_client","error_description":"client amzn1.application-oa2-client.test"}""", tokenStatus)
                : api(request, body);
        }
    }
}
