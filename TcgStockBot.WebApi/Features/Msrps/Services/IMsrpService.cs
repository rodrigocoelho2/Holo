using TcgStockBot.Library.Services;
using TcgStockBot.WebApi.Features.Msrps.Models.DTOs;

namespace TcgStockBot.WebApi.Features.Msrps.Services;

/// <summary>
/// MSRP reference prices per product kind.
/// </summary>
internal interface IMsrpService
{
    /// <summary>
    /// Loads the MSRP table (data/msrp.json, or the built-in defaults) as a lookup.
    /// </summary>
    /// <returns>The lookup.</returns>
    Task<MsrpLookup> LookupAsync();

    /// <summary>
    /// The product kinds and the MSRP table, for the dashboard.
    /// </summary>
    /// <returns>The kinds and prices.</returns>
    Task<ServiceApiResponse<MsrpListDTO>> ListAsync();
}
