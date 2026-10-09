using TcgStockBot.Library.Enums;
using TcgStockBot.WebApi.Features.Stores.Models.Entities;

namespace TcgStockBot.WebApi.Features.Stores.Services.Adapters;

/// <summary>
/// A product as a store's search returned it — the normalized result of every provider, whatever the source
/// (storefront search, official API or affiliate feed).
/// </summary>
/// <param name="StoreName">The store it came from.</param>
/// <param name="Title">The product title.</param>
/// <param name="Url">The product page.</param>
/// <param name="Price">The price, when shown.</param>
/// <param name="InStock">Whether the product can be bought.</param>
/// <param name="ProductId">The store's identifier (ASIN, SKU, EAN…), when the source provides one.</param>
/// <param name="Currency">ISO 4217 currency of <paramref name="Price"/>.</param>
/// <param name="Seller">Who sells it (e.g. the store itself or a marketplace seller), when known.</param>
/// <param name="PreOrder">Whether the provider's data says it is a pre-order (titles are checked separately).</param>
/// <param name="ImageUrl">An absolute http(s) URL of the product photo, when the source provides one.</param>
public sealed record StoreListing(
    string StoreName,
    string Title,
    Uri Url,
    decimal? Price,
    bool InStock,
    string? ProductId = null,
    string Currency = "EUR",
    string? Seller = null,
    bool PreOrder = false,
    string? ImageUrl = null);

/// <summary>
/// Gets product listings from one kind of store (a platform or a store-specific provider).
/// Implementations own their authentication, requests, parsing, normalization and error handling;
/// the rest of the bot never knows whether data came from a search page, an API or a feed.
/// </summary>
internal interface IStoreAdapter
{
    /// <summary>Gets the platform this adapter handles.</summary>
    StorePlatform Platform { get; }

    /// <summary>
    /// Runs a search on a store.
    /// </summary>
    /// <param name="store">The store configuration.</param>
    /// <param name="query">The search text.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>What the store currently lists.</returns>
    Task<IReadOnlyList<StoreListing>> SearchAsync(Store store, string query, CancellationToken cancellationToken);
}

/// <summary>
/// Picks the adapter matching a store's platform.
/// </summary>
internal interface IStoreAdapterResolver
{
    /// <summary>
    /// Resolves the adapter of a store.
    /// </summary>
    /// <param name="store">The store.</param>
    /// <returns>The adapter.</returns>
    IStoreAdapter Resolve(Store store);
}

/// <summary>
/// Default <see cref="IStoreAdapterResolver"/>.
/// </summary>
/// <param name="adapters">Every registered adapter.</param>
internal sealed class StoreAdapterResolver(IEnumerable<IStoreAdapter> adapters) : IStoreAdapterResolver
{
    private readonly Dictionary<StorePlatform, IStoreAdapter> _adapters = adapters.ToDictionary(static adapter => adapter.Platform);

    /// <inheritdoc />
    public IStoreAdapter Resolve(Store store) =>
        _adapters.TryGetValue(store.Platform, out var adapter)
            ? adapter
            : throw new InvalidOperationException($"No adapter registered for platform {store.Platform} (store '{store.Name}').");
}
