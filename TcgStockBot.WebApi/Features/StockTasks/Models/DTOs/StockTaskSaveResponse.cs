namespace TcgStockBot.WebApi.Features.StockTasks.Models.DTOs;

/// <summary>
/// Output of StockTaskSave.
/// </summary>
public sealed class StockTaskSaveResponse
{
    /// <summary>Gets or sets the identifier of the saved task.</summary>
    public string Id { get; set; } = string.Empty;
}
