using TcgStockBot.WebApi.Features.Stores.Services.Adapters;

namespace TcgStockBot.WebApi.Features.Alerts.Models;

/// <summary>
/// A listing that satisfies a stock task right now (in-memory, during a check cycle).
/// </summary>
/// <param name="StockTaskId">The task identifier.</param>
/// <param name="StockTaskName">The task name.</param>
/// <param name="Listing">The matching listing.</param>
/// <param name="LanguageToConfirm">Whether the title had no language marker and must be checked on the page.</param>
public sealed record StockAlert(string StockTaskId, string StockTaskName, StoreListing Listing, bool LanguageToConfirm)
{
    /// <summary>Gets the key identifying the same product on the same store for the same task, across cycles.</summary>
    public string Key => $"{SearchKey(Listing.StoreName, StockTaskId)}|{Listing.Url.GetLeftPart(UriPartial.Path)}";

    /// <summary>
    /// Identifies one search: one task on one store.
    /// </summary>
    /// <param name="storeName">The store name.</param>
    /// <param name="stockTaskId">The task identifier.</param>
    /// <returns>"store|task".</returns>
    public static string SearchKey(string storeName, string stockTaskId) => $"{storeName}|{stockTaskId}";
}
