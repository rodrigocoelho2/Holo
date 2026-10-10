using TcgStockBot.Library.Enums;

namespace TcgStockBot.WebApi.Features.Msrps.Models.DTOs;

/// <summary>
/// The product kinds the bot recognises in titles, and the MSRP reference price of each (per game).
/// </summary>
public sealed class MsrpListDTO
{
    /// <summary>Gets or sets every product kind, in the order the classifier tries them ("other" last).</summary>
    public List<ProductKindDTO> Kinds { get; set; } = [];

    /// <summary>Gets or sets the MSRP reference prices.</summary>
    public List<MsrpDTO> Entries { get; set; } = [];
}

/// <summary>A product kind (Elite Trainer Box, Booster Bundle…).</summary>
public sealed class ProductKindDTO
{
    /// <summary>Gets or sets the code used in tasks ("etb").</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Gets or sets the label ("Elite Trainer Box").</summary>
    public string Label { get; set; } = string.Empty;
}

/// <summary>The MSRP reference price of one product kind of one game.</summary>
public sealed class MsrpDTO
{
    /// <summary>Gets or sets the game ("Pokémon").</summary>
    public string Game { get; set; } = string.Empty;

    /// <summary>Gets or sets the product kind code.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>Gets or sets the price in EUR.</summary>
    public decimal Price { get; set; }

    /// <summary>Gets or sets how the price was obtained.</summary>
    public MsrpBasis Basis { get; set; }

    /// <summary>Gets or sets where the price comes from.</summary>
    public string Source { get; set; } = string.Empty;
}
