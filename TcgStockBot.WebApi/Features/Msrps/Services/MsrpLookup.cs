using TcgStockBot.Library.Text;
using TcgStockBot.WebApi.Features.Msrps.Models.Entities;

namespace TcgStockBot.WebApi.Features.Msrps.Services;

/// <summary>
/// Finds the reference price of a product title: the usual MSRP of its kind for the game named in the title or the
/// task. Built once per use from the current reference prices (immutable afterwards).
/// </summary>
/// <param name="entries">The reference prices.</param>
internal sealed class MsrpLookup(IEnumerable<MsrpEntry> entries)
{
    private readonly ILookup<string, MsrpEntry> _byKind = entries
        .Where(static entry => entry.Price > 0)
        .ToLookup(static entry => entry.Kind, StringComparer.OrdinalIgnoreCase);

    /// <summary>A lookup without prices.</summary>
    public static MsrpLookup Empty { get; } = new([]);

    /// <summary>
    /// The reference price of a product.
    /// </summary>
    /// <param name="title">The store's product title (decides the kind).</param>
    /// <param name="context">Other text naming the game, e.g. the task name ("Pokémon Pitch Black (EN)").</param>
    /// <returns>The reference price of the kind for the game named, or null (unknown kind, or another game —
    /// a One Piece box never gets a Pokémon price).</returns>
    public MsrpEntry? Find(string title, string? context = null)
    {
        var kind = ProductKindClassifier.Classify(title);
        if (kind == ProductKindClassifier.Other || !_byKind.Contains(kind.Code))
        {
            return null;
        }

        var text = $" {Words(title)} {Words(context ?? string.Empty)} ";
        return _byKind[kind.Code].FirstOrDefault(entry => text.Contains($" {Words(entry.Game)} ", StringComparison.Ordinal));
    }

    private static string Words(string text) => string.Join(' ', TextNormalizer.Words(text));
}
