namespace TcgStockBot.WebApi.Features.Stores.Models.DTOs;

/// <summary>
/// Output of StoreSave.
/// </summary>
public sealed class StoreSaveResponse
{
    /// <summary>Gets or sets the name of the saved store.</summary>
    public string Name { get; set; } = string.Empty;
}
