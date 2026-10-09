namespace TcgStockBot.Library.Enums;

/// <summary>
/// How trustworthy an MSRP reference price is. There is no official EUR MSRP list for Portugal / Spain, so most
/// euro values are launch prices seen at big retailers.
/// </summary>
public enum MsrpBasis
{
    /// <summary>Published by the publisher / official store (Pokémon Center).</summary>
    Official,

    /// <summary>The price big retailers sold it at on release.</summary>
    LaunchPrice,

    /// <summary>Derived (e.g. from the US MSRP or the usual price of the kind); confirm it.</summary>
    Estimate,
}
