using TcgStockBot.Library.Enums;

namespace TcgStockBot.WebApi.Features.StockTasks.Models.Entities;

/// <summary>
/// A product to watch, as stored in bot-config.json ("wishlist" items). Never leaves the service layer — map to <c>StockTaskDTO</c>.
/// </summary>
public sealed class StockTask
{
    /// <summary>Gets or sets the stable identifier (used as the alert memory key).</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets the display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether the task is searched.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets the stores the task runs on; empty means every enabled store.</summary>
    public List<string> StoreNames { get; set; } = [];

    /// <summary>Gets or sets the text sent to each store's search.</summary>
    public string SearchQuery { get; set; } = string.Empty;

    /// <summary>Gets or sets the keywords that must all be in the title.</summary>
    public List<string> RequiredKeywords { get; set; } = [];

    /// <summary>Gets or sets the keywords that must not be in the title.</summary>
    public List<string> ExcludedKeywords { get; set; } = [];

    /// <summary>Gets or sets the price ceiling (MSRP).</summary>
    public decimal MaxPrice { get; set; }

    /// <summary>Gets or sets the print run wanted. Missing in older files, which then default to English.</summary>
    public TaskLanguage Language { get; set; } = TaskLanguage.English;
}
