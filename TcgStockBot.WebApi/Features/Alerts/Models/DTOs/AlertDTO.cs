using TcgStockBot.WebApi.Features.Alerts.Models.Entities;

namespace TcgStockBot.WebApi.Features.Alerts.Models.DTOs;

/// <summary>
/// Read model of a sent alert.
/// </summary>
public sealed class AlertDTO
{
    /// <summary>
    /// Initializes an empty DTO (serialization).
    /// </summary>
    public AlertDTO()
    {
    }

    /// <summary>
    /// Maps a history entry to the read model.
    /// </summary>
    /// <param name="entity">The stored entry.</param>
    public AlertDTO(AlertHistory entity)
    {
        At = entity.At;
        StockTaskId = entity.WishlistItemId;
        StockTaskName = entity.WishlistItemName;
        StoreName = entity.StoreName;
        Title = entity.Title;
        Url = entity.Url;
        Price = entity.Price;
        LanguageToConfirm = entity.LanguageToConfirm;
        PreviousPrice = entity.PreviousPrice;
        PreOrder = entity.PreOrder;
        Msrp = entity.Msrp;
    }

    /// <summary>Gets or sets when the alert was sent (UTC).</summary>
    public DateTimeOffset At { get; set; }

    /// <summary>Gets or sets the stock task identifier.</summary>
    public string StockTaskId { get; set; } = string.Empty;

    /// <summary>Gets or sets the stock task name.</summary>
    public string StockTaskName { get; set; } = string.Empty;

    /// <summary>Gets or sets the store name.</summary>
    public string StoreName { get; set; } = string.Empty;

    /// <summary>Gets or sets the product title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets the product page URL.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>Gets or sets the price at alert time.</summary>
    public decimal? Price { get; set; }

    /// <summary>Gets or sets a value indicating whether the language must be confirmed on the page.</summary>
    public bool LanguageToConfirm { get; set; }

    /// <summary>Gets or sets the price of the previous alert, for a price-drop alert (null otherwise).</summary>
    public decimal? PreviousPrice { get; set; }

    /// <summary>Gets or sets a value indicating whether the product was a pre-order.</summary>
    public bool PreOrder { get; set; }

    /// <summary>Gets or sets the MSRP reference price at alert time (null when unknown).</summary>
    public decimal? Msrp { get; set; }
}
