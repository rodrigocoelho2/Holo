namespace TcgStockBot.WebApi.Features.Msrps.Services;

/// <summary>
/// The MSRP reference prices, used by Compare and the alerts ("−8% vs MSRP").
/// </summary>
internal interface IMsrpService
{
    /// <summary>
    /// A lookup over the current reference prices.
    /// </summary>
    /// <returns>The lookup.</returns>
    Task<MsrpLookup> LookupAsync();
}
