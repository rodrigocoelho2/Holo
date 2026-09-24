using TcgStockBot.Library.Services;
using TcgStockBot.WebApi.Features.StockTasks.Models.DTOs;

namespace TcgStockBot.WebApi.Features.StockTasks.Services;

/// <summary>
/// Business logic for stock tasks.
/// </summary>
internal interface IStockTaskService
{
    /// <summary>
    /// Lists every task.
    /// </summary>
    /// <returns>The tasks.</returns>
    Task<ServiceApiResponse<List<StockTaskDTO>>> ListAsync();

    /// <summary>
    /// Gets one task.
    /// </summary>
    /// <param name="identifier">The task identifier.</param>
    /// <returns>The task, or not found.</returns>
    Task<ServiceApiResponse<StockTaskDTO>> GetAsync(string identifier);

    /// <summary>
    /// Creates or replaces a task.
    /// </summary>
    /// <param name="item">The task data.</param>
    /// <returns>The saved identifier, or validation problems.</returns>
    Task<ServiceApiResponse<StockTaskSaveResponse>> SaveAsync(StockTaskSaveRequest item);

    /// <summary>
    /// Deletes a task.
    /// </summary>
    /// <param name="identifier">The task identifier.</param>
    /// <returns>Success, or validation problems.</returns>
    Task<ServiceApiResponse<string>> DeleteAsync(string identifier);

    /// <summary>
    /// Starts or stops one task, or every task.
    /// </summary>
    /// <param name="item">What to change.</param>
    /// <returns>Success, or validation problems.</returns>
    Task<ServiceApiResponse<string>> EnableAsync(StockTaskEnableRequest item);
}
