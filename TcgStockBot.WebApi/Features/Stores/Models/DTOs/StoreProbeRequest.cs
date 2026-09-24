namespace TcgStockBot.WebApi.Features.Stores.Models.DTOs;

/// <summary>
/// Input of StoreProbe: one search on a store configuration, saved or not.
/// </summary>
public sealed class StoreProbeRequest
{
    /// <summary>Gets or sets the store configuration to test.</summary>
    public StoreSaveRequest Store { get; set; } = new();

    /// <summary>Gets or sets the text to search.</summary>
    public string Query { get; set; } = string.Empty;
}
