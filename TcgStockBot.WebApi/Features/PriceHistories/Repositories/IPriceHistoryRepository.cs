using TcgStockBot.WebApi.Features.PriceHistories.Models.Entities;

namespace TcgStockBot.WebApi.Features.PriceHistories.Repositories;

/// <summary>
/// Data access for price-history.json.
/// </summary>
internal interface IPriceHistoryRepository
{
    /// <summary>
    /// Loads every history (key → entry).
    /// </summary>
    /// <returns>The histories; empty when the file does not exist yet.</returns>
    Task<Dictionary<string, PriceHistoryEntry>> ListAsync();

    /// <summary>
    /// Replaces every history.
    /// </summary>
    /// <param name="entries">The histories.</param>
    /// <returns>A task that completes when saved.</returns>
    Task SaveAsync(Dictionary<string, PriceHistoryEntry> entries);
}
