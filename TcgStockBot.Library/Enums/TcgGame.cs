namespace TcgStockBot.Library.Enums;

/// <summary>
/// The trading card game a stock task belongs to. Used to group and filter tasks; matching is still done by
/// the search text and keywords.
/// </summary>
public enum TcgGame
{
    /// <summary>Pokémon TCG (the default, and what tasks saved before this field existed are).</summary>
    Pokemon,

    /// <summary>One Piece Card Game.</summary>
    OnePiece,

    /// <summary>Disney Lorcana.</summary>
    Lorcana,

    /// <summary>Magic: The Gathering.</summary>
    Magic,

    /// <summary>Any other game.</summary>
    Other,
}
