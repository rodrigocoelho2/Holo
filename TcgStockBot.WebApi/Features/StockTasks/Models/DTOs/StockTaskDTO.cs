using TcgStockBot.Library.Enums;
using TcgStockBot.WebApi.Features.StockTasks.Models.Entities;

namespace TcgStockBot.WebApi.Features.StockTasks.Models.DTOs;

/// <summary>
/// Read model of a stock task.
/// </summary>
public sealed class StockTaskDTO
{
    /// <summary>
    /// Initializes an empty DTO (serialization).
    /// </summary>
    public StockTaskDTO()
    {
    }

    /// <summary>
    /// Maps an entity to the read model.
    /// </summary>
    /// <param name="entity">The stored task.</param>
    public StockTaskDTO(StockTask entity)
    {
        Id = entity.Id;
        Name = entity.Name;
        Enabled = entity.Enabled;
        StoreNames = [.. entity.StoreNames];
        SearchQuery = entity.SearchQuery;
        RequiredKeywords = [.. entity.RequiredKeywords];
        ExcludedKeywords = [.. entity.ExcludedKeywords];
        MaxPrice = entity.MaxPrice;
        PriceRule = entity.PriceRule;
        MsrpTolerancePercent = entity.MsrpTolerancePercent;
        Kinds = [.. entity.Kinds];
        Language = entity.Language;
        Game = entity.Game;
    }

    /// <summary>Gets or sets the identifier.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets the display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether the task runs.</summary>
    public bool Enabled { get; set; }

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
    public PriceRule PriceRule { get; set; }

    /// <summary>Gets or sets how far above the MSRP still alerts, in percent.</summary>
    public int MsrpTolerancePercent { get; set; }

    /// <summary>Gets or sets the product kinds wanted; empty means every kind.</summary>
    public List<string> Kinds { get; set; } = [];

    /// <summary>Gets or sets the print run wanted (English or Japanese).</summary>
    public TaskLanguage Language { get; set; }

    /// <summary>Gets or sets the card game.</summary>
    public TcgGame Game { get; set; }
}
