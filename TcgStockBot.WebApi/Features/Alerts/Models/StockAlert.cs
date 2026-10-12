using TcgStockBot.WebApi.Features.Stores.Services.Adapters;

namespace TcgStockBot.WebApi.Features.Alerts.Models;

/// <summary>
/// A listing that satisfies a stock task right now (in-memory, during a check cycle).
/// </summary>
/// <param name="StockTaskId">The task identifier.</param>
/// <param name="StockTaskName">The task name.</param>
/// <param name="Listing">The matching listing.</param>
/// <param name="LanguageToConfirm">Whether the title had no language marker and must be checked on the page.</param>
/// <param name="PreviousPrice">For a price-drop alert, the price of the previous alert; null for a new product.</param>
/// <param name="Msrp">The MSRP reference price of the product, when known.</param>
/// <param name="Shipping">What the store charges to ship it (0 = free), when the store's shipping is known.</param>
public sealed record StockAlert(string StockTaskId, string StockTaskName, StoreListing Listing, bool LanguageToConfirm, decimal? PreviousPrice = null, decimal? Msrp = null, decimal? Shipping = null)
{
    /// <summary>Gets a value indicating whether this alert is a price drop on a product that was already alerted.</summary>
    public bool IsPriceDrop => PreviousPrice is not null;

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
