using TcgStockBot.Sdk;
using TcgStockBot.Sdk.Models;

namespace TcgStockBot.Website.Model.Stores;

/// <summary>
/// Store operations for the dashboard.
/// </summary>
/// <param name="apiClient">The API client.</param>
public sealed class StoreModel(TcgStockBotClient apiClient) : WebsiteBaseModel(apiClient)
{
    /// <summary>Lists every store.</summary>
    /// <returns>The stores.</returns>
    public Task<AjaxDataResult<List<StoreDTO>>> ListAsync() => CallAsync(() => ApiClient.StoreListAsync());

    /// <summary>Creates, replaces or renames a store.</summary>
    /// <param name="item">The store data.</param>
    /// <returns>The saved name.</returns>
    public Task<AjaxDataResult<StoreSaveResponse>> SaveAsync(StoreSaveRequest item) => CallAsync(() => ApiClient.StoreSaveAsync(item));

    /// <summary>Deletes a store.</summary>
    /// <param name="identifier">The store name.</param>
    /// <returns>Success.</returns>
    public Task<AjaxDataResult<bool>> DeleteAsync(string identifier) => CallAsync(() => ApiClient.StoreDeleteAsync(identifier));

    /// <summary>Detects how a store can be searched from its address.</summary>
    /// <param name="item">The store address.</param>
    /// <returns>The suggested configuration.</returns>
    public Task<AjaxDataResult<StoreDetectResult>> DetectAsync(StoreDetectRequest item) => CallAsync(() => ApiClient.StoreDetectAsync(item));

    /// <summary>Tests one saved store.</summary>
    /// <param name="item">The store and the search text.</param>
    /// <returns>The test result.</returns>
    public Task<AjaxDataResult<StoreTestResultDTO>> TestAsync(StoreTestRequest item) => CallAsync(() => ApiClient.StoreTestAsync(item));

    /// <summary>Turns a store on or off.</summary>
    /// <param name="item">What to change.</param>
    /// <returns>Success.</returns>
    public Task<AjaxDataResult<bool>> EnableAsync(StoreEnableRequest item) => CallAsync(() => ApiClient.StoreEnableAsync(item));

    /// <summary>Runs one search on a store configuration.</summary>
    /// <param name="item">The store and the search text.</param>
    /// <returns>The listings with their outcomes.</returns>
    public Task<AjaxDataResult<List<StoreProbeResult>>> ProbeAsync(StoreProbeRequest item) => CallAsync(() => ApiClient.StoreProbeAsync(item));
}
