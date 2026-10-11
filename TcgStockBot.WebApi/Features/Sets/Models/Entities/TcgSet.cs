using TcgStockBot.Library.Enums;

namespace TcgStockBot.WebApi.Features.Sets.Models.Entities;

/// <summary>
/// One set (expansion) of a game: what a product belongs to, so the same set is one product family whatever task
/// found it. The built-in list lives in Features/Sets/Data/set-defaults.json; the releases (releases.json) add theirs.
/// </summary>
public sealed class TcgSet
{
    /// <summary>Gets or sets the stable identifier ("pkm-me05").</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets the name as the stores write it ("Pitch Black").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the game.</summary>
    public TcgGame Game { get; set; } = TcgGame.Pokemon;

    /// <summary>Gets or sets the set code ("ME05", "OP-13"), when the game has one.</summary>
    public string? Code { get; set; }

    /// <summary>Gets or sets the English release date, when known.</summary>
    public DateOnly? ReleaseDate { get; set; }

    /// <summary>Gets or sets other ways stores write the set ("mega evolution 5").</summary>
    public List<string> Aliases { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether the name is also the name of the series ("Mega Evolution",
    /// "Scarlet &amp; Violet"): it only counts when no other set of the game is named in the title.
    /// </summary>
    public bool Series { get; set; }
}
