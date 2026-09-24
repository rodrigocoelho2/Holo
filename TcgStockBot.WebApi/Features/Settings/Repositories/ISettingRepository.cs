using TcgStockBot.WebApi.Features.Settings.Models.Entities;

namespace TcgStockBot.WebApi.Features.Settings.Repositories;

/// <summary>
/// Data access for the monitoring settings.
/// </summary>
internal interface ISettingRepository
{
    /// <summary>
    /// Gets the settings.
    /// </summary>
    /// <returns>The settings.</returns>
    Task<Setting> GetAsync();

    /// <summary>
    /// Replaces the settings.
    /// </summary>
    /// <param name="item">The settings.</param>
    /// <returns>The stored settings.</returns>
    Task<Setting> SaveAsync(Setting item);

    /// <summary>
    /// Changes only the pause flag.
    /// </summary>
    /// <param name="paused">The new value.</param>
    /// <returns>A task that completes when saved.</returns>
    Task SetPausedAsync(bool paused);
}
