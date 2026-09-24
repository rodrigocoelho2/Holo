using TcgStockBot.WebApi.Features.Stores.Models.Entities;

namespace TcgStockBot.WebApi.Features.Stores.Repositories;

/// <summary>
/// Data access for stores.
/// </summary>
internal interface IStoreRepository
{
    /// <summary>
    /// Lists every store.
    /// </summary>
    /// <returns>The stores, in stored order.</returns>
    Task<List<Store>> ListAsync();

    /// <summary>
    /// Gets one store.
    /// </summary>
    /// <param name="name">The store name (case-insensitive).</param>
    /// <returns>The store, or null when it does not exist.</returns>
    Task<Store?> GetAsync(string name);

    /// <summary>
    /// Inserts a store, or replaces the one called <paramref name="originalName"/> (keeping its position).
    /// </summary>
    /// <param name="item">The store.</param>
    /// <param name="originalName">The name of the store being replaced; null to insert.</param>
    /// <returns>The stored store.</returns>
    Task<Store> SaveAsync(Store item, string? originalName);

    /// <summary>
    /// Deletes a store (no-op when it does not exist).
    /// </summary>
    /// <param name="name">The store name.</param>
    /// <returns>A task that completes when saved.</returns>
    Task DeleteAsync(string name);

    /// <summary>
    /// Sets the Enabled flag of a store.
    /// </summary>
    /// <param name="name">The store name.</param>
    /// <param name="enabled">The new value.</param>
    /// <returns>A task that completes when saved.</returns>
    Task SetEnabledAsync(string name, bool enabled);
}
