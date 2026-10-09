namespace TcgStockBot.Library.Enums;

/// <summary>
/// Where a store sells from — the dashboard groups the stores by it.
/// </summary>
public enum StoreCountry
{
    /// <summary>A Portuguese store.</summary>
    Portugal,

    /// <summary>A Spanish store (check it ships to Portugal).</summary>
    Spain,

    /// <summary>Another European store or an EU-wide channel (Pokémon Center, marketplaces).</summary>
    Europe,
}
