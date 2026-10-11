namespace TcgStockBot.WebApi.Features.PriceHistories.Models.Entities;

/// <summary>
/// One product seen during a check cycle (in memory), to be added to its price history.
/// </summary>
/// <param name="StoreName">The store name.</param>
/// <param name="Title">The product title.</param>
/// <param name="Url">The product page URL.</param>
/// <param name="ProductId">The store's product identifier, when known.</param>
/// <param name="Currency">ISO 4217 currency.</param>
/// <param name="Price">The price.</param>
/// <param name="InStock">Whether it can be bought.</param>
/// <param name="StockTaskId">The task that recognised it (null when unknown).</param>
public sealed record PriceObservation(string StoreName, string Title, string Url, string? ProductId, string Currency, decimal Price, bool InStock, string? StockTaskId = null, string? ImageUrl = null)
{
    /// <summary>Gets the history key of this product.</summary>
    public string Key => KeyFor(StoreName, Url);

    /// <summary>
    /// The history key: one product on one store, whatever task found it. Query strings are ignored.
    /// </summary>
    /// <param name="storeName">The store name.</param>
    /// <param name="url">The product page URL.</param>
    /// <returns>"store|url-without-query" (lower case).</returns>
    public static string KeyFor(string storeName, string url) =>
        $"{storeName}|{(Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.GetLeftPart(UriPartial.Path) : url)}".ToLowerInvariant();
}
