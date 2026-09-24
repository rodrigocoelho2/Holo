namespace TcgStockBot.WebApi.Features.Stores.Models.DTOs;

/// <summary>
/// Input of StoreEnable.
/// </summary>
public sealed class StoreEnableRequest
{
    /// <summary>Gets or sets the store name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether the store should be searched.</summary>
    public bool Enabled { get; set; }
}
