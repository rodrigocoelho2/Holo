using TcgStockBot.WebApi.Features.Releases.Models.Entities;

namespace TcgStockBot.WebApi.Features.Releases.Repositories;

/// <summary>
/// Data access for releases.json.
/// </summary>
internal interface IReleaseRepository
{
    /// <summary>
    /// Loads every release.
    /// </summary>
    /// <returns>The releases; empty when the file does not exist yet.</returns>
    Task<List<Release>> ListAsync();

    /// <summary>
    /// Replaces every release.
    /// </summary>
    /// <param name="releases">The releases.</param>
    /// <returns>A task that completes when saved.</returns>
    Task SaveAsync(List<Release> releases);
}
