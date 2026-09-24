using TcgStockBot.Sdk;
using TcgStockBot.Sdk.Models;

namespace TcgStockBot.Website.Model.Settings;

/// <summary>
/// Settings operations for the dashboard.
/// </summary>
/// <param name="apiClient">The API client.</param>
public sealed class SettingModel(TcgStockBotClient apiClient) : WebsiteBaseModel(apiClient)
{
    /// <summary>Gets the settings.</summary>
    /// <returns>The settings.</returns>
    public Task<AjaxDataResult<SettingDTO>> GetAsync() => CallAsync(() => ApiClient.SettingGetAsync());

    /// <summary>Saves the settings.</summary>
    /// <param name="item">The new settings.</param>
    /// <returns>The saved settings.</returns>
    public Task<AjaxDataResult<SettingDTO>> SaveAsync(SettingSaveRequest item) => CallAsync(() => ApiClient.SettingSaveAsync(item));
}
