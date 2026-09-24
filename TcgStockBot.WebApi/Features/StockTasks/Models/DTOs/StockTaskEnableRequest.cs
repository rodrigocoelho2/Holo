namespace TcgStockBot.WebApi.Features.StockTasks.Models.DTOs;

/// <summary>
/// Input of StockTaskEnable: starts or stops one task, or every task.
/// </summary>
public sealed class StockTaskEnableRequest
{
    /// <summary>Gets or sets the task identifier; null or empty applies to every task.</summary>
    public string? Id { get; set; }

    /// <summary>Gets or sets a value indicating whether the task(s) should run.</summary>
    public bool Enabled { get; set; }
}
