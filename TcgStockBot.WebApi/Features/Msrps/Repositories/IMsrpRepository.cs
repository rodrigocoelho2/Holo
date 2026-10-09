using TcgStockBot.WebApi.Features.Msrps.Models.Entities;

namespace TcgStockBot.WebApi.Features.Msrps.Repositories;

/// <summary>
/// The reference prices: data/msrp.json when it exists, else the researched defaults shipped with the bot.
/// </summary>
internal interface IMsrpRepository
{
    /// <summary>
    /// Lists the reference prices.
    /// </summary>
    /// <returns>The reference prices.</returns>
    Task<List<MsrpEntry>> ListAsync();
}
