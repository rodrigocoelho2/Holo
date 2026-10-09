using TcgStockBot.Sdk;
using TcgStockBot.Sdk.Models;

namespace TcgStockBot.Website.Model.Compares;

/// <summary>
/// Compare operations for the dashboard.
/// </summary>
/// <param name="apiClient">The API client.</param>
public sealed class CompareModel(TcgStockBotClient apiClient) : WebsiteBaseModel(apiClient)
{
    /// <summary>Lists every compared product.</summary>
    /// <returns>The groups.</returns>
    public Task<AjaxDataResult<List<CompareGroupDTO>>> ListAsync() => CallAsync(() => ApiClient.CompareListAsync());

    /// <summary>Gets one compared product with its price points.</summary>
    /// <param name="identifier">The group key.</param>
    /// <returns>The group.</returns>
    public Task<AjaxDataResult<CompareGroupDTO>> GetAsync(string identifier) => CallAsync(() => ApiClient.CompareGetAsync(identifier ?? string.Empty));

    /// <summary>Corrects the grouping of one listing.</summary>
    /// <param name="item">The listing and its target group.</param>
    /// <returns>Success.</returns>
    public Task<AjaxDataResult<bool>> AssignAsync(CompareAssignRequest item) => CallAsync(() => ApiClient.CompareAssignAsync(item));
}
