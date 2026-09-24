using System.Globalization;
using System.Net;
using System.Text.Json;
using TcgStockBot.Library.Enums;
using TcgStockBot.WebApi.Features.Stores.Models.Entities;

namespace TcgStockBot.WebApi.Features.Stores.Services.Adapters;

/// <summary>
/// Uses Shopify's public predictive-search endpoint (/search/suggest.json), which every Shopify storefront
/// exposes and which returns title, URL, price and availability in one call.
/// </summary>
/// <param name="httpClientFactory">Creates the store HttpClient.</param>
internal sealed class ShopifyStoreAdapter(IHttpClientFactory httpClientFactory) : IStoreAdapter
{
    /// <inheritdoc />
    public StorePlatform Platform => StorePlatform.Shopify;

    /// <inheritdoc />
    public async Task<IReadOnlyList<StoreListing>> SearchAsync(Store store, string query, CancellationToken cancellationToken)
    {
        var baseUri = new Uri(store.BaseUrl);
        var searchUri = new Uri(baseUri,
            $"/search/suggest.json?q={Uri.EscapeDataString(query)}" +
            "&resources[type]=product&resources[limit]=10&resources[options][unavailable_products]=last");

        using var response = await httpClientFactory.CreateClient(StoreHttpClient.Name).GetAsync(searchUri, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var content = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(content, cancellationToken: cancellationToken);

        if (!document.RootElement.TryGetProperty("resources", out var resources)
            || !resources.TryGetProperty("results", out var results)
            || !results.TryGetProperty("products", out var products))
        {
            return [];
        }

        var listings = new List<StoreListing>();
        foreach (var product in products.EnumerateArray())
        {
            var title = WebUtility.HtmlDecode(product.GetProperty("title").GetString() ?? string.Empty);
            var url = new Uri(baseUri, product.GetProperty("url").GetString() ?? "/");
            var inStock = product.TryGetProperty("available", out var available) && available.ValueKind == JsonValueKind.True;

            // Suggest URLs carry "?_pos=1&_sid=…" tracking parameters that change on every call.
            listings.Add(new StoreListing(store.Name, title, new Uri(url.GetLeftPart(UriPartial.Path)), ReadPrice(product), inStock));
        }

        return listings;
    }

    /// <summary>Shopify returns "price" as a decimal string ("54.99"); a few themes return a number.</summary>
    private static decimal? ReadPrice(JsonElement product)
    {
        if (!product.TryGetProperty("price", out var price))
        {
            return null;
        }

        return price.ValueKind switch
        {
            JsonValueKind.Number => price.GetDecimal(),
            JsonValueKind.String when decimal.TryParse(price.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => null,
        };
    }
}
