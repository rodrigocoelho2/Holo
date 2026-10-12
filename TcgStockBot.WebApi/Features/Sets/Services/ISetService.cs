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

    /// <summary>
    /// Lists every set: the built-in ones (with what the releases add) and the ones only a release names.
    /// </summary>
    /// <returns>The sets; <see cref="Models.Entities.TcgSet.ReleaseId"/> tells which ones a release covers.</returns>
    Task<List<Models.Entities.TcgSet>> ListAsync();
}
