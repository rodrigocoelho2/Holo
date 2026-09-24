using TcgStockBot.Sdk;
using TcgStockBot.Sdk.Models;

namespace TcgStockBot.Website.Model.Monitors;

/// <summary>
/// Monitor operations for the dashboard.
/// </summary>
/// <param name="apiClient">The API client.</param>
public sealed class MonitorModel(TcgStockBotClient apiClient) : WebsiteBaseModel(apiClient)
{
    /// <summary>Gets the live state.</summary>
    /// <returns>The monitor state.</returns>
    public Task<AjaxDataResult<MonitorDTO>> GetAsync() => CallAsync(() => ApiClient.MonitorGetAsync());

    /// <summary>Asks for a check now.</summary>
    /// <returns>Success.</returns>
    public Task<AjaxDataResult<bool>> RunAsync() => CallAsync(() => ApiClient.MonitorRunAsync());

    /// <summary>Pauses the bot.</summary>
    /// <returns>Success.</returns>
    public Task<AjaxDataResult<bool>> PauseAsync() => CallAsync(() => ApiClient.MonitorPauseAsync());

    /// <summary>Resumes the bot.</summary>
    /// <returns>Success.</returns>
    public Task<AjaxDataResult<bool>> ResumeAsync() => CallAsync(() => ApiClient.MonitorResumeAsync());
}
