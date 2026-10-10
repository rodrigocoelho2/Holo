using Microsoft.AspNetCore.Mvc;
using TcgStockBot.Sdk;
using TcgStockBot.Website.Model.Msrps;

namespace TcgStockBot.Website.Controllers;

/// <summary>
/// MSRP and product kind actions of the dashboard.
/// </summary>
/// <param name="apiClient">The API client.</param>
public partial class MsrpController(TcgStockBotClient apiClient) : WebsiteBaseController(apiClient)
{
    /// <summary>The product kinds and the MSRP table.</summary>
    /// <returns>JSON <c>AjaxDataResult</c>.</returns>
    [HttpGet]
    public async Task<IActionResult> ListAsync()
    {
        var model = new MsrpModel(ApiClient);
        return Json(await model.ListAsync());
    }
}
