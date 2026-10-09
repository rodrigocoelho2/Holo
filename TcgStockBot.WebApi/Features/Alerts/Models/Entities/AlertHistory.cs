namespace TcgStockBot.WebApi.Features.Alerts.Models.Entities;

/// <summary>
/// One sent alert, as stored in alert-history.json. Property names are the file format — do not rename.
/// </summary>
public sealed class AlertHistory
{
    /// <summary>Gets or sets when the alert was sent (UTC).</summary>
    public DateTimeOffset At { get; set; }

    /// <summary>Gets or sets the stock task identifier.</summary>
    public string WishlistItemId { get; set; } = string.Empty;

    /// <summary>Gets or sets the stock task name.</summary>
    public string WishlistItemName { get; set; } = string.Empty;

    /// <summary>Gets or sets the store name.</summary>
    public string StoreName { get; set; } = string.Empty;

    /// <summary>Gets or sets the product title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets the product page URL.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>Gets or sets the price at alert time.</summary>
    public decimal? Price { get; set; }

    /// <summary>Gets or sets a value indicating whether the title had no language marker (confirm on the page).</summary>
    public bool LanguageToConfirm { get; set; }

    /// <summary>Gets or sets the price of the previous alert, for a price-drop alert (null otherwise).</summary>
    public decimal? PreviousPrice { get; set; }

    /// <summary>Gets or sets a value indicating whether the product was a pre-order.</summary>
    public bool PreOrder { get; set; }

    /// <summary>Gets or sets the MSRP reference price at alert time (null when unknown).</summary>
    public decimal? Msrp { get; set; }
}
