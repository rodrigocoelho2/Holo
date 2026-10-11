namespace TcgStockBot.WebApi.Features.Sets.Services;

/// <summary>
/// The sets of every game: the built-in list plus the releases.
/// </summary>
internal interface ISetService
{
    /// <summary>
    /// Builds the lookup that finds the set of a product title.
    /// </summary>
    /// <returns>The lookup.</returns>
    Task<SetLookup> LookupAsync();
}
