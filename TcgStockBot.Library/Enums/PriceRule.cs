namespace TcgStockBot.Library.Enums;

/// <summary>
/// How a stock task decides that a price is good enough.
/// </summary>
public enum PriceRule
{
    /// <summary>A fixed maximum price for everything the task finds (what tasks saved before this field existed use).</summary>
    MaxPrice,

    /// <summary>
    /// Up to the MSRP of each product kind plus a tolerance (an ETB against the ETB MSRP, a booster against the booster
    /// MSRP). Kinds without an MSRP use the fixed maximum when there is one.
    /// </summary>
    Msrp,
}
