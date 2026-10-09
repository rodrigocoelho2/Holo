using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using TcgStockBot.Library.Enums;
using TcgStockBot.Library.Text;
using TcgStockBot.WebApi.Features.Stores.Models.Entities;

namespace TcgStockBot.WebApi.Features.Stores.Services.Adapters;

/// <summary>
/// Shopify storefronts, in one of two modes:
/// <list type="bullet">
/// <item><b>Search</b> (no catalog paths): the public predictive search (/search/suggest.json) — one request per task,
/// but Shopify returns at most 10 products.</item>
/// <item><b>Catalog</b> (<see cref="Store.ShopifyCatalogPaths"/>): the public product JSON of the whole store or of a
/// few collections (/collections/pokemon/products.json), read page by page, kept for <see cref="CatalogLifetime"/>
/// and filtered locally for every task — no 10-product limit and one read per cycle for all tasks.</item>
/// </list>
/// </summary>
/// <param name="httpClientFactory">Creates the store HttpClient.</param>
/// <param name="timeProvider">Clock for the catalog cache (defaults to the system clock).</param>
internal sealed class ShopifyStoreAdapter(IHttpClientFactory httpClientFactory, TimeProvider? timeProvider = null) : IStoreAdapter
{
    /// <summary>How long a downloaded catalog is reused: long enough for one cycle's tasks, short enough to stay fresh.</summary>
    internal static readonly TimeSpan CatalogLifetime = TimeSpan.FromMinutes(3);

    /// <summary>Products per page (Shopify's maximum).</summary>
    internal const int PageSize = 250;

    /// <summary>Safety limit per catalog path: 40 pages = 10 000 products.</summary>
    internal const int MaxPages = 40;

    private const int MaxResults = 100;

    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;
    private readonly ConcurrentDictionary<string, CatalogState> _catalogs = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public StorePlatform Platform => StorePlatform.Shopify;

    /// <inheritdoc />
    public async Task<IReadOnlyList<StoreListing>> SearchAsync(Store store, string query, CancellationToken cancellationToken)
    {
        var paths = CatalogPaths(store.ShopifyCatalogPaths);
        if (paths.Count == 0)
        {
            return await PredictiveSearchAsync(store, query, cancellationToken);
        }

        var catalog = await GetCatalogAsync(store, paths, cancellationToken);
        var words = TextNormalizer.Words(query).Select(static word => $" {word} ").ToList();

        return catalog
            .Where(product => words.All(product.SearchText.Contains))
            .Take(MaxResults)
            .Select(static product => product.Listing)
            .ToList();
    }

    /// <summary>
    /// Splits the configured paths ("/collections/a, /collections/b" or "/products.json") into product JSON paths.
    /// </summary>
    /// <param name="configured">The store setting.</param>
    /// <returns>The paths, each ending in products.json.</returns>
    internal static IReadOnlyList<string> CatalogPaths(string? configured) =>
        (configured ?? string.Empty)
            .Split([',', ';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(static path => "/" + path.Trim('/'))
            .Select(static path => path.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? path : $"{path}/products.json")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>
    /// Maps one page of /products.json. Available when any variant is; the price is the cheapest available variant
    /// (or the cheapest variant when none is available).
    /// </summary>
    /// <param name="store">The store.</param>
    /// <param name="root">The page JSON.</param>
    /// <returns>The products of the page.</returns>
    internal static List<CatalogProduct> MapCatalogPage(Store store, JsonElement root)
    {
        var baseUri = new Uri(store.BaseUrl);
        var products = new List<CatalogProduct>();
        if (!root.TryGetProperty("products", out var items) || items.ValueKind != JsonValueKind.Array)
        {
            return products;
        }

        foreach (var product in items.EnumerateArray())
        {
            var title = WebUtility.HtmlDecode(GetString(product, "title") ?? string.Empty).Trim();
            var handle = GetString(product, "handle");
            if (title.Length == 0 || string.IsNullOrWhiteSpace(handle))
            {
                continue;
            }

            var variants = product.TryGetProperty("variants", out var variantArray) && variantArray.ValueKind == JsonValueKind.Array
                ? variantArray.EnumerateArray()
                    .Select(static variant => (Price: ParsePrice(variant, "price"), Available: variant.TryGetProperty("available", out var available) && available.ValueKind == JsonValueKind.True))
                    .ToList()
                : [];

            var inStock = variants.Any(static variant => variant.Available);
            var price = variants.Where(variant => !inStock || variant.Available).Select(static variant => variant.Price).Where(static value => value is not null).Min();

            var tags = product.TryGetProperty("tags", out var tagArray) && tagArray.ValueKind == JsonValueKind.Array
                ? string.Join(' ', tagArray.EnumerateArray().Select(static tag => tag.GetString()))
                : GetString(product, "tags") ?? string.Empty;
            var searchText = $" {string.Join(' ', TextNormalizer.Words($"{title} {GetString(product, "product_type")} {GetString(product, "vendor")} {tags}"))} ";
            var id = product.TryGetProperty("id", out var idElement) ? idElement.ToString() : null;

            products.Add(new CatalogProduct(
                new StoreListing(store.Name, title, new Uri(baseUri, $"/products/{Uri.EscapeDataString(handle)}"), price, inStock, id),
                searchText));
        }

        return products;
    }

    private async Task<IReadOnlyList<CatalogProduct>> GetCatalogAsync(Store store, IReadOnlyList<string> paths, CancellationToken cancellationToken)
    {
        var key = $"{store.Name}|{store.BaseUrl}|{string.Join(',', paths)}";
        var state = _catalogs.GetOrAdd(key, static _ => new CatalogState());

        if (TryCached(state) is { } cached)
        {
            return cached;
        }

        await state.Lock.WaitAsync(cancellationToken);
        try
        {
            if (TryCached(state) is { } fresh)
            {
                return fresh;
            }

            try
            {
                var products = new Dictionary<string, CatalogProduct>(StringComparer.OrdinalIgnoreCase);
                foreach (var path in paths)
                {
                    await ReadPathAsync(store, path, products, cancellationToken);
                }

                state.Products = [.. products.Values];
                state.LoadedAt = _timeProvider.GetUtcNow();
                state.Failure = null;
                return state.Products;
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                // The other tasks of this cycle get the same error instead of asking the store again.
                state.Failure = ExceptionDispatchInfo.Capture(exception);
                state.FailedAt = _timeProvider.GetUtcNow();
                throw;
            }
        }
        finally
        {
            state.Lock.Release();
        }
    }

    private IReadOnlyList<CatalogProduct>? TryCached(CatalogState state)
    {
        var now = _timeProvider.GetUtcNow();
        if (state.Failure is { } failure && now - state.FailedAt < CatalogLifetime)
        {
            failure.Throw();
        }

        return state.Products is { } products && now - state.LoadedAt < CatalogLifetime ? products : null;
    }

    private async Task ReadPathAsync(Store store, string path, Dictionary<string, CatalogProduct> products, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(StoreHttpClient.Name);
        var baseUri = new Uri(store.BaseUrl);

        for (var page = 1; page <= MaxPages; page++)
        {
            using var response = await client.GetAsync(new Uri(baseUri, $"{path}?limit={PageSize}&page={page}"), cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var content = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(content, cancellationToken: cancellationToken);

            var pageProducts = MapCatalogPage(store, document.RootElement);
            foreach (var product in pageProducts)
            {
                products.TryAdd(product.Listing.Url.AbsoluteUri, product);   // the same product in two collections counts once
            }

            if (pageProducts.Count < PageSize)
            {
                return;
            }
        }
    }

    private async Task<IReadOnlyList<StoreListing>> PredictiveSearchAsync(Store store, string query, CancellationToken cancellationToken)
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
            listings.Add(new StoreListing(store.Name, title, new Uri(url.GetLeftPart(UriPartial.Path)), ParsePrice(product, "price"), inStock));
        }

        return listings;
    }

    /// <summary>Shopify returns prices as decimal strings ("54.99"); a few themes return a number.</summary>
    private static decimal? ParsePrice(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var price))
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

    private static string? GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    /// <summary>A catalog product and the text searched for it (title, type, vendor and tags, normalized and padded with spaces).</summary>
    internal sealed record CatalogProduct(StoreListing Listing, string SearchText);

    private sealed class CatalogState
    {
        public SemaphoreSlim Lock { get; } = new(1, 1);

        public IReadOnlyList<CatalogProduct>? Products { get; set; }

        public DateTimeOffset LoadedAt { get; set; }

        public ExceptionDispatchInfo? Failure { get; set; }

        public DateTimeOffset FailedAt { get; set; }
    }
}
