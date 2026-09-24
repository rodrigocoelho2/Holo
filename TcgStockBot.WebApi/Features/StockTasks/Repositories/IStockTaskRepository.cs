using TcgStockBot.WebApi.Features.StockTasks.Models.Entities;

namespace TcgStockBot.WebApi.Features.StockTasks.Repositories;

/// <summary>
/// Data access for stock tasks.
/// </summary>
internal interface IStockTaskRepository
{
    /// <summary>
    /// Lists every task.
    /// </summary>
    /// <returns>The tasks, in stored order.</returns>
    Task<List<StockTask>> ListAsync();

    /// <summary>
    /// Gets one task.
    /// </summary>
    /// <param name="identifier">The task identifier (case-insensitive).</param>
    /// <returns>The task, or null when it does not exist.</returns>
    Task<StockTask?> GetAsync(string identifier);

    /// <summary>
    /// Inserts the task, or replaces the one with the same identifier.
    /// </summary>
    /// <param name="item">The task.</param>
    /// <returns>The stored task.</returns>
    Task<StockTask> SaveAsync(StockTask item);

    /// <summary>
    /// Deletes a task (no-op when it does not exist).
    /// </summary>
    /// <param name="identifier">The task identifier.</param>
    /// <returns>A task that completes when saved.</returns>
    Task DeleteAsync(string identifier);

    /// <summary>
    /// Sets the Enabled flag of one task, or of all tasks.
    /// </summary>
    /// <param name="identifier">The task identifier; null for all.</param>
    /// <param name="enabled">The new value.</param>
    /// <returns>A task that completes when saved.</returns>
    Task SetEnabledAsync(string? identifier, bool enabled);

    /// <summary>
    /// Replaces a store name in every task's store list.
    /// </summary>
    /// <param name="oldName">The current store name.</param>
    /// <param name="newName">The new store name.</param>
    /// <returns>A task that completes when saved.</returns>
    Task RenameStoreAsync(string oldName, string newName);

    /// <summary>
    /// Removes a store name from every task's store list.
    /// </summary>
    /// <param name="storeName">The store name.</param>
    /// <returns>A task that completes when saved.</returns>
    Task RemoveStoreAsync(string storeName);
}
