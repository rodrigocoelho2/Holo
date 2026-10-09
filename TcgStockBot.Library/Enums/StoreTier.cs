namespace TcgStockBot.Library.Enums;

/// <summary>
/// How interesting a store is for buying at MSRP — a label for the dashboard, it does not change how the store is searched.
/// </summary>
public enum StoreTier
{
    /// <summary>Not classified.</summary>
    None,

    /// <summary>MSRP hunters: big retailers that sell at the recommended price on release.</summary>
    S,

    /// <summary>TCG specialists: reliable stock, usually a little above MSRP.</summary>
    A,

    /// <summary>Importers and smaller online shops.</summary>
    B,

    /// <summary>Marketplaces: market price reference only, not comparable with MSRP.</summary>
    C,
}
