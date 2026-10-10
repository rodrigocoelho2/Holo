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

    /// <summary>Gets or sets the fixed price ceiling; with <see cref="PriceRule.Msrp"/> only for kinds without an MSRP (0 = none).</summary>
    public decimal MaxPrice { get; set; }

    /// <summary>Gets or sets how a price is judged. Missing in older files, which then use the fixed maximum.</summary>
    public PriceRule PriceRule { get; set; } = PriceRule.MaxPrice;

    /// <summary>Gets or sets how far above the MSRP still alerts, in percent (10 = up to MSRP + 10%), for <see cref="PriceRule.Msrp"/>.</summary>
    public int MsrpTolerancePercent { get; set; } = 10;

    /// <summary>Gets or sets the product kinds wanted (ProductKindClassifier codes: etb, booster-bundle…); empty means every kind.</summary>
    public List<string> Kinds { get; set; } = [];

    /// <summary>Gets or sets the print run wanted. Missing in older files, which then default to English.</summary>
    public TaskLanguage Language { get; set; } = TaskLanguage.English;

    /// <summary>Gets or sets the card game. Missing in older files, which then default to Pokémon.</summary>
    public TcgGame Game { get; set; } = TcgGame.Pokemon;
}
