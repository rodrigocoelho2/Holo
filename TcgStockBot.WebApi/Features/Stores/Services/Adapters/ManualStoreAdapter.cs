using TcgStockBot.Library.Enums;
using TcgStockBot.WebApi.Features.Stores.Models.Entities;

namespace TcgStockBot.WebApi.Features.Stores.Services.Adapters;

/// <summary>
/// A store kept in the list only as a reference (no integration). The check cycle skips it; a manual test explains why.
/// </summary>
internal sealed class ManualStoreAdapter : IStoreAdapter
{
    /// <inheritdoc />
    public StorePlatform Platform => StorePlatform.Manual;

    /// <inheritdoc />
    public Task<IReadOnlyList<StoreListing>> SearchAsync(Store store, string query, CancellationToken cancellationToken) =>
        throw new StoreWithoutIntegrationException(store.Name);
}

/// <summary>
/// Raised when a store without integration (Manual platform) is searched.
/// </summary>
/// <param name="storeName">The store name.</param>
public sealed class StoreWithoutIntegrationException(string storeName) : Exception($"{storeName} has no integration.")
{
    /// <summary>Gets the store name.</summary>
    public string StoreName { get; } = storeName;
}
