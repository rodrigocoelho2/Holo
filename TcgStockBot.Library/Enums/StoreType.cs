namespace TcgStockBot.Library.Enums;

/// <summary>
/// What kind of shop a store is — a label for the dashboard, it does not change how the store is searched.
/// </summary>
public enum StoreType
{
    /// <summary>Not classified.</summary>
    Unknown,

    /// <summary>A general retailer (supermarket, electronics, toys, department store).</summary>
    Retail,

    /// <summary>A TCG / game specialist shop.</summary>
    Specialist,

    /// <summary>An importer or online reseller (often JP / other markets).</summary>
    Importer,

    /// <summary>A marketplace of third-party sellers (Cardmarket, eBay…).</summary>
    Marketplace,

    /// <summary>The official channel of the publisher (Pokémon Center).</summary>
    Official,
}
