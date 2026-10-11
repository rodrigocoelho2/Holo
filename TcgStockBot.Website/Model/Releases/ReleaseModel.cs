using TcgStockBot.Sdk;
using TcgStockBot.Sdk.Models;

namespace TcgStockBot.Website.Model.Releases;

/// <summary>
/// Launch calendar operations for the dashboard.
/// </summary>
/// <param name="apiClient">The API client.</param>
public sealed class ReleaseModel(TcgStockBotClient apiClient) : WebsiteBaseModel(apiClient)
{
    /// <summary>Lists every release.</summary>
    /// <returns>The releases.</returns>
    public Task<AjaxDataResult<List<ReleaseDTO>>> ListAsync() => CallAsync(() => ApiClient.ReleaseListAsync());

    /// <summary>Creates or replaces a release.</summary>
    /// <param name="item">The release.</param>
    /// <returns>The saved release.</returns>
    public Task<AjaxDataResult<ReleaseDTO>> SaveAsync(ReleaseSaveRequest item) => CallAsync(() => ApiClient.ReleaseSaveAsync(item));

    /// <summary>Deletes a release.</summary>
    /// <param name="identifier">The release identifier.</param>
    /// <returns>Success.</returns>
    public Task<AjaxDataResult<bool>> DeleteAsync(string identifier) => CallAsync(() => ApiClient.ReleaseDeleteAsync(identifier ?? string.Empty));
}
