namespace TcgStockBot.WebApi.Features.PriceHistories.Models.DTOs;

/// <summary>
/// When one store restocks, learnt from the price history: every time a product went from sold out to in stock.
/// Products that come back within a few hours of each other count as one restock (one delivery).
/// </summary>
public sealed class StoreRestockDTO
{
    /// <summary>Gets or sets the store name.</summary>
    public string StoreName { get; set; } = string.Empty;

    /// <summary>Gets or sets how many restocks (deliveries) were seen.</summary>
    public int Restocks { get; set; }

    /// <summary>Gets or sets how many products came back in stock, counting each time.</summary>
    public int Products { get; set; }

    /// <summary>Gets or sets how many days of history this is based on.</summary>
    public int Days { get; set; }

    /// <summary>Gets or sets the last restock (UTC).</summary>
    public DateTimeOffset? LastRestockAt { get; set; }

    /// <summary>Gets or sets the restocks per weekday, Monday first (local time).</summary>
    public List<int> ByWeekday { get; set; } = [];

    /// <summary>Gets or sets the restocks per hour of the day, 0–23 (local time).</summary>
    public List<int> ByHour { get; set; } = [];

    /// <summary>Gets or sets the restocks per weekday and hour: 7 rows (Monday first) of 24 hours.</summary>
    public List<List<int>> ByWeekdayHour { get; set; } = [];

    /// <summary>Gets or sets the weekdays most restocks fall on (0 = Monday), busiest first; empty when there is no pattern.</summary>
    public List<int> UsualWeekdays { get; set; } = [];

    /// <summary>Gets or sets the first hour of the two-hour window most restocks fall in, or null when there is no pattern.</summary>
    public int? UsualHour { get; set; }

    /// <summary>Gets or sets a value indicating whether there are enough restocks to trust the pattern.</summary>
    public bool Confident { get; set; }

    /// <summary>Gets or sets the time zone the hours are in ("Europe/Lisbon").</summary>
    public string TimeZone { get; set; } = string.Empty;
}
