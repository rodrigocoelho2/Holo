using TcgStockBot.Library.Enums;

namespace TcgStockBot.WebApi.Features.Msrps.Models.Entities;

/// <summary>
/// One reference price: the usual MSRP of a kind of product of a game ("Pokémon" + "etb"), for any set.
/// Kept in Features/Msrps/Data/msrp-defaults.json (or data/msrp.json, which replaces it when present).
/// </summary>
public sealed class MsrpEntry
{
    /// <summary>Gets or sets the game the price applies to ("Pokémon").</summary>
    public string Game { get; set; } = "Pokémon";

    /// <summary>Gets or sets the kind of product code ("etb", "booster-bundle"…; see ProductKindClassifier).</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>Gets or sets the reference price in euros.</summary>
    public decimal Price { get; set; }

    /// <summary>Gets or sets the US MSRP in dollars, for reference (optional).</summary>
    public decimal? UsdPrice { get; set; }

    /// <summary>Gets or sets how trustworthy the euro price is.</summary>
    public MsrpBasis Basis { get; set; } = MsrpBasis.Estimate;

    /// <summary>Gets or sets where the price comes from (URL or text).</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>Gets or sets free notes.</summary>
    public string Notes { get; set; } = string.Empty;
}
