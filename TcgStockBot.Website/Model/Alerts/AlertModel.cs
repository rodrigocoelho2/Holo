using TcgStockBot.Sdk;
using TcgStockBot.Sdk.Models;

namespace TcgStockBot.Website.Model.Alerts;

/// <summary>
/// Alert operations for the dashboard.
/// </summary>
/// <param name="apiClient">The API client.</param>
public sealed class AlertModel(TcgStockBotClient apiClient) : WebsiteBaseModel(apiClient)
{
    /// <summary>Lists the alert history.</summary>
    /// <returns>The alerts.</returns>
    public Task<AjaxDataResult<List<AlertDTO>>> ListAsync() => CallAsync(() => ApiClient.AlertListAsync());

    /// <summary>Forgets every alerted product.</summary>
    /// <returns>Success.</returns>
    public Task<AjaxDataResult<bool>> ResetAsync() => CallAsync(() => ApiClient.AlertResetAsync());

    /// <summary>Empties the alert list.</summary>
    /// <returns>Success.</returns>
    public Task<AjaxDataResult<bool>> ClearAsync() => CallAsync(() => ApiClient.AlertClearAsync());
}
