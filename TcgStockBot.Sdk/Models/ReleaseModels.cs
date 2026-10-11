using TcgStockBot.Library.Enums;

namespace TcgStockBot.Sdk.Models;

/// <summary>A product launch in the calendar (mirror of the WebApi ReleaseDTO).</summary>
public sealed class ReleaseDTO
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public TcgGame Game { get; set; }
    public DateOnly ReleaseDate { get; set; }
    public DateOnly? PreOrderDate { get; set; }
    public string? Url { get; set; }
    public string? Notes { get; set; }
    public string? StockTaskId { get; set; }

    /// <summary>Gets or sets other ways stores write the set.</summary>
    public List<string> Aliases { get; set; } = [];
}

/// <summary>Creates (empty id) or replaces a release.</summary>
public sealed class ReleaseSaveRequest
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public TcgGame Game { get; set; }
    public DateOnly? ReleaseDate { get; set; }
    public DateOnly? PreOrderDate { get; set; }
    public string? Url { get; set; }
    public string? Notes { get; set; }
    public string? StockTaskId { get; set; }

    /// <summary>Gets or sets other ways stores write the set.</summary>
    public List<string> Aliases { get; set; } = [];
}
