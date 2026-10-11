using TcgStockBot.Library.Enums;

namespace TcgStockBot.WebApi.Features.Releases.Models.DTOs;

/// <summary>A product launch in the calendar.</summary>
public sealed class ReleaseDTO
{
    /// <summary>Gets or sets the identifier.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the game.</summary>
    public TcgGame Game { get; set; }

    /// <summary>Gets or sets the release date.</summary>
    public DateOnly ReleaseDate { get; set; }

    /// <summary>Gets or sets when pre-orders open, when known.</summary>
    public DateOnly? PreOrderDate { get; set; }

    /// <summary>Gets or sets an optional page with the official details.</summary>
    public string? Url { get; set; }

    /// <summary>Gets or sets free notes.</summary>
    public string? Notes { get; set; }

    /// <summary>Gets or sets the stock task that watches this release.</summary>
    public string? StockTaskId { get; set; }

    /// <summary>Gets or sets other ways stores write the set.</summary>
    public List<string> Aliases { get; set; } = [];
}

/// <summary>Creates (empty <see cref="Id"/>) or replaces a release.</summary>
public sealed class ReleaseSaveRequest
{
    /// <summary>Gets or sets the identifier; empty for a new release.</summary>
    public string? Id { get; set; }

    /// <summary>Gets or sets the name.</summary>
    public string? Name { get; set; }

    /// <summary>Gets or sets the game.</summary>
    public TcgGame Game { get; set; }

    /// <summary>Gets or sets the release date.</summary>
    public DateOnly? ReleaseDate { get; set; }

    /// <summary>Gets or sets when pre-orders open.</summary>
    public DateOnly? PreOrderDate { get; set; }

    /// <summary>Gets or sets an optional page with the official details.</summary>
    public string? Url { get; set; }

    /// <summary>Gets or sets free notes.</summary>
    public string? Notes { get; set; }

    /// <summary>Gets or sets the stock task that watches this release.</summary>
    public string? StockTaskId { get; set; }

    /// <summary>Gets or sets other ways stores write the set.</summary>
    public List<string> Aliases { get; set; } = [];
}
