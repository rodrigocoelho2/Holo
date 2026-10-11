using TcgStockBot.Library.Enums;

namespace TcgStockBot.WebApi.Features.Releases.Models.Entities;

/// <summary>
/// A product launch the user wants to remember (releases.json): when it comes out, when pre-orders open
/// and, optionally, the stock task that watches it.
/// </summary>
public sealed class Release
{
    /// <summary>Gets or sets the stable identifier.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets the name ("Pokémon — Ascended Heroes").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the game.</summary>
    public TcgGame Game { get; set; } = TcgGame.Pokemon;

    /// <summary>Gets or sets the release date.</summary>
    public DateOnly ReleaseDate { get; set; }

    /// <summary>Gets or sets when pre-orders open, when known.</summary>
    public DateOnly? PreOrderDate { get; set; }

    /// <summary>Gets or sets an optional page with the official details.</summary>
    public string? Url { get; set; }

    /// <summary>Gets or sets free notes (products in the wave, MSRP…).</summary>
    public string? Notes { get; set; }

    /// <summary>Gets or sets the stock task that watches this release, when there is one.</summary>
    public string? StockTaskId { get; set; }
}
