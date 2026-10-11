using TcgStockBot.WebApi.Features.Sets.Models.Entities;

namespace TcgStockBot.WebApi.Features.Sets.Repositories;

/// <summary>
/// The built-in sets of every game.
/// </summary>
internal interface ISetRepository
{
    /// <summary>
    /// Lists the built-in sets.
    /// </summary>
    /// <returns>The sets (a new list on every call).</returns>
    Task<List<TcgSet>> ListAsync();
}
