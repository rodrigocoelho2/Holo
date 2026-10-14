using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using TcgStockBot.Library.Enums;
using TcgStockBot.WebApi.Extensions.Http;
using TcgStockBot.WebApi.Features.Stores.Models.Entities;

namespace TcgStockBot.WebApi.Features.Stores.Services.Adapters.Amazon;

/// <summary>
/// Amazon (amazon.es) through the official Creators API — the successor of PA-API 5. No scraping.
/// A search text that is an ASIN (or "asin:XXXXXXXXXX") uses GetItems; anything else uses SearchItems.
/// Requires an Associates account with Creators API access (see AmazonCreatorsOptions).
/// </summary>
/// <param name="options">Amazon settings.</param>
/// <param name="tokenProvider">Cached OAuth token.</param>
/// <param name="httpClientFactory">Creates the outbound client.</param>
internal sealed partial class AmazonCreatorsStoreAdapter(
    IOptionsMonitor<AmazonCreatorsOptions> options,
    AmazonCreatorsTokenProvider tokenProvider,
    IHttpClientFactory httpClientFactory) : IStoreAdapter
{
    /// <summary>The named HttpClient for the Creators API (and its token endpoint).</summary>
    public const string HttpClientName = "amazon-creators";

    private const string ProviderName = "Amazon";

    private static readonly string[] Resources =
    [
        "itemInfo.title",
        "offersV2.listings.price",
        "offersV2.listings.availability",
        "offersV2.listings.merchantInfo",
        "offersV2.listings.isBuyBoxWinner",
        "images.primary.large",
    ];

    /// <summary>Availability types that mean "can be ordered now" (pre-orders included).</summary>
    private static readonly HashSet<string> PurchasableAvailability =
        new(StringComparer.OrdinalIgnoreCase) { "IN_STOCK", "IN_STOCK_SCARCE", "PREORDER", "LEADTIME", "AVAILABLE_DATE" };

    /// <inheritdoc />
    public StorePlatform Platform => StorePlatform.Amazon;

    /// <inheritdoc />
    public async Task<IReadOnlyList<StoreListing>> SearchAsync(Store store, string query, CancellationToken cancellationToken)
    {
        var settings = options.CurrentValue;
        var asin = ParseAsin(query);

        var (path, body) = asin is not null
            ? ("/catalog/v1/getItems", (object)new
            {
                itemIds = new[] { asin },
                itemIdType = "ASIN",
                partnerTag = settings.PartnerTag.Trim(),
                marketplace = settings.Marketplace,
                resources = Resources,
            })
            : ("/catalog/v1/searchItems", new
            {
                keywords = query.Trim(),
                itemCount = 10,
                partnerTag = settings.PartnerTag.Trim(),
                marketplace = settings.Marketplace,
                resources = Resources,
            });

        using var document = await SendAsync(store, settings, path, body, cancellationToken, retryOnUnauthorized: true);
        return Map(store, document.RootElement);
    }

    /// <summary>
    /// Maps a SearchItems/GetItems response to listings.
    /// </summary>
    /// <param name="store">The store.</param>
    /// <param name="root">The response JSON.</param>
    /// <returns>The listings.</returns>
    internal static IReadOnlyList<StoreListing> Map(Store store, JsonElement root)
    {
        // SearchItems → searchResult.items; GetItems → itemsResult.items (SDK) or itemResults.items (docs sample).
        var items = FirstArray(root, ["searchResult", "items"], ["itemsResult", "items"], ["itemResults", "items"]);
        if (items is null)
        {
            return [];
        }

        var listings = new List<StoreListing>();
        foreach (var item in items.Value.EnumerateArray())
        {
            var asin = GetString(item, "asin");
            var url = GetString(item, "detailPageURL");
            var title = item.TryGetProperty("itemInfo", out var info) && info.TryGetProperty("title", out var titleElement)
                ? GetString(titleElement, "displayValue")
                : null;

            if (string.IsNullOrWhiteSpace(title) || !Uri.TryCreate(url, UriKind.Absolute, out var productUrl))
            {
                continue;
            }

            var listing = BestListing(item);
            decimal? price = null;
            var currency = "EUR";
            var inStock = false;
            var preOrder = false;
            string? seller = null;

            if (listing is { } offer)
            {
                if (offer.TryGetProperty("price", out var priceElement) && priceElement.TryGetProperty("money", out var money))
                {
                    if (money.TryGetProperty("amount", out var amount) && amount.ValueKind == JsonValueKind.Number)
                    {
                        price = amount.GetDecimal();
                    }

                    currency = GetString(money, "currency") ?? currency;
                }

                var availabilityType = offer.TryGetProperty("availability", out var availability) ? GetString(availability, "type") : null;
                inStock = availabilityType is not null && PurchasableAvailability.Contains(availabilityType);
                preOrder = string.Equals(availabilityType, "PREORDER", StringComparison.OrdinalIgnoreCase);

                seller = offer.TryGetProperty("merchantInfo", out var merchant) ? GetString(merchant, "name") : null;
            }

            listings.Add(new StoreListing(store.Name, title, productUrl, price, inStock, asin, currency, seller, preOrder, ReadImage(item)));
        }

        return listings;
    }

    /// <summary>The "images.primary.large" resource: { "images": { "primary": { "large": { "url": … } } } }.</summary>
    private static string? ReadImage(JsonElement item) =>
        item.TryGetProperty("images", out var images)
        && images.ValueKind == JsonValueKind.Object
        && images.TryGetProperty("primary", out var primary)
        && primary.ValueKind == JsonValueKind.Object
        && primary.TryGetProperty("large", out var large)
            ? StoreImageUrl.Normalize(GetString(large, "url"))
            : null;

    /// <summary>The ASIN in a search text: a bare 10-character ASIN ("B0…") or "asin:XXXXXXXXXX".</summary>
    internal static string? ParseAsin(string query)
    {
        var match = AsinPattern().Match(query.Trim());
        return match.Success ? match.Groups["asin"].Value.ToUpperInvariant() : null;
    }

    private async Task<JsonDocument> SendAsync(Store store, AmazonCreatorsOptions settings, string path, object body, CancellationToken cancellationToken, bool retryOnUnauthorized)
    {
        if (!settings.IsConfigured)
        {
            throw new StoreIntegrationException(ProviderName, StoreIntegrationProblem.NotConfigured,
                "set Amazon__CredentialId, Amazon__CredentialSecret and Amazon__PartnerTag (Associates Central → Creators API).");
        }

        var token = await tokenProvider.GetTokenAsync(cancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(settings.ApiBaseUrl), path))
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.TryAddWithoutValidation("x-marketplace", settings.Marketplace);
        request.ForProvider(ProviderName, RateLimitDomain(store));

        using var response = await httpClientFactory.CreateClient(HttpClientName).SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized && retryOnUnauthorized)
        {
            // The token may have been revoked or expired early: refresh once, then give up.
            tokenProvider.Invalidate();
            return await SendAsync(store, settings, path, body, cancellationToken, retryOnUnauthorized: false);
        }

        if (response.StatusCode is HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500)
        {
            response.EnsureSuccessStatusCode();   // → "asked to slow down" / "store error"; the outbound handler already backed off
        }

        var content = await response.Content.ReadAsStreamAsync(cancellationToken);
        var document = await JsonDocument.ParseAsync(content, cancellationToken: cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var type = GetString(document.RootElement, "type") ?? $"HTTP {(int)response.StatusCode}";
            var reason = GetString(document.RootElement, "reason");
            var message = GetString(document.RootElement, "message");
            document.Dispose();

            var problem = response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
                ? StoreIntegrationProblem.Unauthorized
                : StoreIntegrationProblem.ApiError;
            throw new StoreIntegrationException(ProviderName, problem, string.Join(" · ", new[] { type, reason, message }.Where(static part => !string.IsNullOrWhiteSpace(part))));
        }

        return document;
    }

    /// <summary>Calls count against the store's own domain ("amazon.es") in OutboundHttp:RateLimits.</summary>
    private static string RateLimitDomain(Store store) =>
        Uri.TryCreate(store.BaseUrl, UriKind.Absolute, out var baseUri) && baseUri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase)
            ? baseUri.Host[4..]
            : Uri.TryCreate(store.BaseUrl, UriKind.Absolute, out var other) ? other.Host : "amazon.es";

    /// <summary>The buy-box offer, or the first one.</summary>
    private static JsonElement? BestListing(JsonElement item)
    {
        if (!item.TryGetProperty("offersV2", out var offers) || !offers.TryGetProperty("listings", out var listings)
            || listings.ValueKind != JsonValueKind.Array || listings.GetArrayLength() == 0)
        {
            return null;
        }

        foreach (var listing in listings.EnumerateArray())
        {
            if (listing.TryGetProperty("isBuyBoxWinner", out var winner) && winner.ValueKind == JsonValueKind.True)
            {
                return listing;
            }
        }

        return listings[0];
    }

    private static JsonElement? FirstArray(JsonElement root, params string[][] paths)
    {
        foreach (var path in paths)
        {
            var current = root;
            var found = true;
            foreach (var name in path)
            {
                if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(name, out current))
                {
                    found = false;
                    break;
                }
            }

            if (found && current.ValueKind == JsonValueKind.Array)
            {
                return current;
            }
        }

        return null;
    }

    private static string? GetString(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    [GeneratedRegex(@"^(?:asin:\s*)?(?<asin>B0[0-9A-Z]{8})$", RegexOptions.IgnoreCase)]
    private static partial Regex AsinPattern();
}
