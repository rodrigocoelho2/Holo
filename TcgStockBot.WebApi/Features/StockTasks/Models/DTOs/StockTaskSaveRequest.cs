using TcgStockBot.Library.Enums;
using TcgStockBot.WebApi.Features.StockTasks.Models.Entities;

namespace TcgStockBot.WebApi.Features.StockTasks.Models.DTOs;

/// <summary>
/// Input of StockTaskSave: creates a task (<see cref="IsNew"/>) or replaces an existing one.
/// </summary>
public sealed class StockTaskSaveRequest
{
    /// <summary>Gets or sets a value indicating whether this creates a new task (the identifier must not exist yet).</summary>
    public bool IsNew { get; set; }

    /// <summary>Gets or sets the identifier. Cannot change once created.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets the display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether the task runs.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets the stores the task runs on (empty = all enabled stores).</summary>
    public List<string> StoreNames { get; set; } = [];

    /// <summary>Gets or sets the text sent to each store's search.</summary>
    public string SearchQuery { get; set; } = string.Empty;

    /// <summary>Gets or sets the keywords that must all be in the title.</summary>
    public List<string> RequiredKeywords { get; set; } = [];

    /// <summary>Gets or sets the keywords that must not be in the title.</summary>
    public List<string> ExcludedKeywords { get; set; } = [];

    /// <summary>Gets or sets the price ceiling.</summary>
    public decimal MaxPrice { get; set; }

    /// <summary>Gets or sets how a price is judged.</summary>
    public PriceRule PriceRule { get; set; } = PriceRule.MaxPrice;

    /// <summary>Gets or sets how far above the MSRP still alerts, in percent.</summary>
    public int MsrpTolerancePercent { get; set; } = 10;

    /// <summary>Gets or sets the product kinds wanted; empty means every kind.</summary>
    public List<string> Kinds { get; set; } = [];

    /// <summary>Gets or sets the print run wanted (English or Japanese).</summary>
    public TaskLanguage Language { get; set; } = TaskLanguage.English;

    /// <summary>Gets or sets the card game.</summary>
    public TcgGame Game { get; set; } = TcgGame.Pokemon;

    /// <summary>
    /// Maps the request to a new entity, trimming text and dropping empty keywords.
    /// </summary>
    /// <returns>The entity to store.</returns>
    public StockTask StockTaskNew() => new()
    {
        Id = Id.Trim(),
        Name = Name.Trim(),
        Enabled = Enabled,
        StoreNames = Clean(StoreNames),
        SearchQuery = SearchQuery.Trim(),
        RequiredKeywords = Clean(RequiredKeywords),
        ExcludedKeywords = Clean(ExcludedKeywords),
        MaxPrice = MaxPrice,
        PriceRule = PriceRule,
        MsrpTolerancePercent = MsrpTolerancePercent,
        Kinds = Clean(Kinds).Select(static kind => kind.ToLowerInvariant()).ToList(),
        Language = Language,
        Game = Game,
    };

    private static List<string> Clean(IEnumerable<string>? values) =>
        (values ?? []).Select(static value => value.Trim()).Where(static value => value.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
}
