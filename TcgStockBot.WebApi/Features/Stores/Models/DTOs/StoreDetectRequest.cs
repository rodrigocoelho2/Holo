namespace TcgStockBot.WebApi.Features.Stores.Models.DTOs;

/// <summary>
/// Input of StoreDetect: the store's address (home page, or a category page to suggest a collection).
/// </summary>
public sealed class StoreDetectRequest
{
    /// <summary>Gets or sets the URL.</summary>
    public string Url { get; set; } = string.Empty;
}
