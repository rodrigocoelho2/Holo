using System.Globalization;
using System.Net;
using System.Text.Json;
using TcgStockBot.Library.Enums;
using TcgStockBot.WebApi.Features.Stores.Models.Entities;

namespace TcgStockBot.WebApi.Features.Stores.Services.Adapters;

/// <summary>
/// Uses the WooCommerce Store API (public and unauthenticated on every WooCommerce shop that hasn't disabled it).
/// </summary>
/// <param name="httpClientFactory">Creates the store HttpClient.</param>
internal sealed class WooCommerceStoreAdapter(IHttpClientFactory httpClientFactory) : IStoreAdapter
{
    /// <inheritdoc />
    public StorePlatform Platform => StorePlatform.WooCommerce;

    /// <inheritdoc />
    public async Task<IReadOnlyList<StoreListing>> SearchAsync(Store store, string query, CancellationToken cancellationToken)
    {
        var baseUri = new Uri(store.BaseUrl);
        var searchUri = new Uri(baseUri, $"{store.WooCommerceProductsPath}?search={Uri.EscapeDataString(query)}&per_page=20");

        using var response = await httpClientFactory.CreateClient(StoreHttpClient.Name).GetAsync(searchUri, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var content = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(content, cancellationToken: cancellationToken);

        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var listings = new List<StoreListing>();
        foreach (var product in document.RootElement.EnumerateArray())
        {
            var title = WebUtility.HtmlDecode(product.GetProperty("name").GetString() ?? string.Empty);
            var url = new Uri(product.GetProperty("permalink").GetString() ?? store.BaseUrl);
            var inStock = IsTrue(product, "is_in_stock") && IsTrue(product, "is_purchasable");

            listings.Add(new StoreListing(store.Name, title, url, ReadPrice(product), inStock));
        }

        return listings;
    }

    private static bool IsTrue(JsonElement product, string propertyName) =>
        product.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.True;

    /// <summary>Prices come in minor units: { "price": "5499", "currency_minor_unit": 2 } = 54.99.</summary>
    private static decimal? ReadPrice(JsonElement product)
    {
        if (!product.TryGetProperty("prices", out var prices)
            || !prices.TryGetProperty("price", out var price)
            || !decimal.TryParse(price.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var minorUnits))
        {
            return null;
        }

        var minorUnit = prices.TryGetProperty("currency_minor_unit", out var unit) && unit.TryGetInt32(out var digits) ? digits : 2;
        return minorUnits / (decimal)Math.Pow(10, minorUnit);
    }
}
