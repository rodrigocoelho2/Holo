namespace TcgStockBot.WebApi.Features.Compares.Repositories;

/// <summary>
/// Data access for compare-overrides.json: the manual corrections of the Compare grouping
/// (price history key → group key, or "solo" for a product of its own).
/// </summary>
internal interface ICompareOverrideRepository
{
    /// <summary>
    /// Loads every correction.
    /// </summary>
    /// <returns>History key → group key; empty when the file does not exist yet.</returns>
    Task<Dictionary<string, string>> ListAsync();

    /// <summary>
    /// Replaces every correction.
    /// </summary>
    /// <param name="overrides">History key → group key.</param>
    /// <returns>A task that completes when saved.</returns>
    Task SaveAsync(Dictionary<string, string> overrides);
}
