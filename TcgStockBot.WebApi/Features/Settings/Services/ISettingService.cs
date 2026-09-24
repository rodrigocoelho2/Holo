using TcgStockBot.Library.Services;
using TcgStockBot.WebApi.Features.Settings.Models.DTOs;

namespace TcgStockBot.WebApi.Features.Settings.Services;

/// <summary>
/// Business logic for the monitoring settings.
/// </summary>
internal interface ISettingService
{
    /// <summary>
    /// Gets the settings.
    /// </summary>
    /// <returns>The settings.</returns>
    Task<ServiceApiResponse<SettingDTO>> GetAsync();

    /// <summary>
    /// Validates and saves the settings.
    /// </summary>
    /// <param name="item">The new settings.</param>
    /// <returns>The saved settings, or validation problems.</returns>
    Task<ServiceApiResponse<SettingDTO>> SaveAsync(SettingSaveRequest item);
}
