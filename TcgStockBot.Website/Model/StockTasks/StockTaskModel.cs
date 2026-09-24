using TcgStockBot.Sdk;
using TcgStockBot.Sdk.Models;

namespace TcgStockBot.Website.Model.StockTasks;

/// <summary>
/// Stock task operations for the dashboard.
/// </summary>
/// <param name="apiClient">The API client.</param>
public sealed class StockTaskModel(TcgStockBotClient apiClient) : WebsiteBaseModel(apiClient)
{
    /// <summary>Lists every task.</summary>
    /// <returns>The tasks.</returns>
    public Task<AjaxDataResult<List<StockTaskDTO>>> ListAsync() => CallAsync(() => ApiClient.StockTaskListAsync());

    /// <summary>Creates or replaces a task.</summary>
    /// <param name="item">The task data.</param>
    /// <returns>The saved identifier.</returns>
    public Task<AjaxDataResult<StockTaskSaveResponse>> SaveAsync(StockTaskSaveRequest item) => CallAsync(() => ApiClient.StockTaskSaveAsync(item));

    /// <summary>Deletes a task.</summary>
    /// <param name="identifier">The task identifier.</param>
    /// <returns>Success.</returns>
    public Task<AjaxDataResult<bool>> DeleteAsync(string identifier) => CallAsync(() => ApiClient.StockTaskDeleteAsync(identifier));

    /// <summary>Starts or stops one task, or every task.</summary>
    /// <param name="item">What to change.</param>
    /// <returns>Success.</returns>
    public Task<AjaxDataResult<bool>> EnableAsync(StockTaskEnableRequest item) => CallAsync(() => ApiClient.StockTaskEnableAsync(item));
}
