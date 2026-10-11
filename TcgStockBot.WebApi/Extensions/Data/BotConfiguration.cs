using TcgStockBot.WebApi.Features.StockTasks.Models.Entities;
using TcgStockBot.WebApi.Features.Stores.Models.Entities;

namespace TcgStockBot.WebApi.Extensions.Data;

/// <summary>
/// The root document stored in data/bot-config.json. Seeded once from the "Bot" section of
/// appsettings.json; afterwards only the repositories change it. Property names are the file format —
/// do not rename them, or existing installations lose their configuration.
/// </summary>
public sealed class BotConfiguration
{
    /// <summary>Gets or sets a value indicating whether the scheduler skips cycles.</summary>
    public bool Paused { get; set; }

    /// <summary>Gets or sets the time between two check cycles.</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>Gets or sets the random extra wait added to each cycle.</summary>
    public TimeSpan PollJitter { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>Gets or sets the pause between two requests to the same store.</summary>
    public TimeSpan DelayBetweenRequests { get; set; } = TimeSpan.FromSeconds(3);

    /// <summary>Gets or sets how many failed cycles in a row turn a store off (0 = never).</summary>
    public int AutoDisableAfterFailures { get; set; } = 6;

    /// <summary>Gets or sets a value indicating whether a summary of the day is e-mailed once a day.</summary>
    public bool DailySummaryEnabled { get; set; }

    /// <summary>Gets or sets the local hour (0–23) of the daily summary.</summary>
    public int DailySummaryHour { get; set; } = 9;

    /// <summary>Gets or sets the User-Agent sent to stores.</summary>
    public string UserAgent { get; set; } = "TcgStockBot/1.0 (personal stock alert)";

    /// <summary>Gets or sets the stock tasks (stored as "wishlist" for compatibility).</summary>
    public List<StockTask> Wishlist { get; set; } = [];

    /// <summary>Gets or sets the stores.</summary>
    public List<Store> Stores { get; set; } = [];
}
