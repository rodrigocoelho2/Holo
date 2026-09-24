using TcgStockBot.WebApi.Features.Alerts.Models.Entities;

namespace TcgStockBot.WebApi.Features.Alerts.Repositories;

/// <summary>
/// Data access for the alert history and the "already alerted" memory.
/// </summary>
internal interface IAlertRepository
{
    /// <summary>
    /// Lists the alert history, newest first.
    /// </summary>
    /// <returns>The history.</returns>
    Task<List<AlertHistory>> ListHistoryAsync();

    /// <summary>
    /// Prepends entries to the history, keeping at most <paramref name="maxEntries"/>.
    /// </summary>
    /// <param name="entries">The new entries (newest first).</param>
    /// <param name="maxEntries">The history size limit.</param>
    /// <returns>A task that completes when saved.</returns>
    Task AddHistoryAsync(IReadOnlyList<AlertHistory> entries, int maxEntries);

    /// <summary>
    /// Loads the memory of currently-alerted products (alert key → first alert time).
    /// </summary>
    /// <returns>The memory.</returns>
    Task<Dictionary<string, DateTimeOffset>> GetStateAsync();

    /// <summary>
    /// Replaces the memory of currently-alerted products.
    /// </summary>
    /// <param name="state">The new memory.</param>
    /// <returns>A task that completes when saved.</returns>
    Task SaveStateAsync(Dictionary<string, DateTimeOffset> state);
}
