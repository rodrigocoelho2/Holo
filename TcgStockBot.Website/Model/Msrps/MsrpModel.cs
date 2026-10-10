using TcgStockBot.Sdk;
using TcgStockBot.Sdk.Models;

namespace TcgStockBot.Website.Model.Msrps;

/// <summary>
/// MSRP reference prices and product kinds for the dashboard.
/// </summary>
/// <param name="apiClient">The API client.</param>
public sealed class MsrpModel(TcgStockBotClient apiClient) : WebsiteBaseModel(apiClient)
{
    /// <summary>The product kinds and the MSRP table.</summary>
    /// <returns>The kinds and prices.</returns>
    public Task<AjaxDataResult<MsrpListDTO>> ListAsync() => CallAsync(() => ApiClient.MsrpListAsync());
}
