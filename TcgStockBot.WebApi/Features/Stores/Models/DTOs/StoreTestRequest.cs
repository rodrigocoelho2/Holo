namespace TcgStockBot.WebApi.Features.Stores.Models.DTOs;

/// <summary>
/// Input of StoreTest: tests one saved store and keeps the result on it.
/// </summary>
public sealed class StoreTestRequest
{
    /// <summary>Gets or sets the store name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the search text ("pokemon" when empty).</summary>
    public string Query { get; set; } = string.Empty;
}
