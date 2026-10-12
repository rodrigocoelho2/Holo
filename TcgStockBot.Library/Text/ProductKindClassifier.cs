using System.Text.RegularExpressions;

namespace TcgStockBot.Library.Text;

/// <summary>
/// What kind of sealed product a title describes, so the same product can be recognised across stores that word
/// their titles differently ("Pitch Black Elite Trainer Box", "PKM - Mega Evolution 5 - Pitch Black ETB").
/// </summary>
/// <param name="Code">Stable identifier ("etb", "booster-display", …).</param>
/// <param name="Label">Name shown in the dashboard.</param>
public sealed record ProductKind(string Code, string Label);

/// <summary>
/// Classifies product titles into <see cref="ProductKind"/>s. Rules run from the most specific to the most generic,
/// on normalized words (accents, case and punctuation ignored).
/// </summary>
public static partial class ProductKindClassifier
{
    /// <summary>Anything not recognised: identified by its distinctive words.</summary>
    public static readonly ProductKind Other = new("other", "Other");

    private static readonly (ProductKind Kind, Func<string, bool> Matches)[] Rules =
    [
        (new("etb-booster-box-combo", "Combo Booster Box + ETB"), static title => HasAny(title, "booster box", "booster display") && HasAny(title, "elite trainer box", "etb")),
        (new("pokemon-center-etb", "Pokémon Center ETB"), static title => Has(title, "pokemon center") && HasAny(title, "elite trainer box", "etb")),
        (new("etb", "Elite Trainer Box"), static title => HasAny(title, "elite trainer box", "etb")),
        (new("booster-bundle", "Booster Bundle"), static title => Has(title, "booster bundle")),
        (new("half-display", "Half Booster Display"), static title =>
            HasAny(title, "booster display", "booster box", "display", "box") && HasAny(title, "half", "meia", "18 boosters", "18 booster", "18 packs", "18 pack")),
        (new("booster-display", "Booster Display"), static title => HasAny(title, "booster display", "booster box", "display", "boosterbox")),
        (new("ultra-premium", "Ultra Premium Collection"), static title => HasAny(title, "ultra premium", "upc")),
        (new("super-premium", "Super Premium Collection"), static title => HasAny(title, "super premium", "spc")),
        (new("build-battle", "Build & Battle"), static title => HasAny(title, "build battle", "build and battle", "build & battle")),
        (new("binder-collection", "Binder Collection"), static title => Has(title, "binder collection")),
        (new("poster-collection", "Poster Collection"), static title => Has(title, "poster collection")),
        (new("sleeved-booster", "Sleeved Booster"), static title => Has(title, "sleeved")),
        (new("blister-3", "Blister 3 boosters"), static title => HasAny(title, "3 pack", "3 booster", "3pack", "tripack", "triple pack")),
        (new("checklane-blister", "Checklane Blister"), static title => Has(title, "checklane")),
        (new("blister-2", "Blister 2 boosters"), static title => HasAny(title, "2 pack", "2pack", "enhanced")),
        (new("mini-tin", "Mini Tin"), static title => Has(title, "mini tin")),
        (new("tin", "Tin"), static title => HasAny(title, "tin", "lata")),
        (new("ex-box", "ex Box"), static title => HasAny(title, "ex box", "ex collection")),
        (new("premium-collection", "Premium Collection"), static title => Has(title, "premium collection")),
        (new("collection", "Collection"), static title => HasAny(title, "collection", "coleccao", "colecao")),
        (new("deck", "Deck"), static title => HasAny(title, "deck", "battle deck", "theme deck")),
        (new("booster-pack", "Booster"), static title => HasAny(title, "booster pack", "booster", "boosters", "pack")),
    ];

    /// <summary>Words that never tell two products apart: game, series, language, store and marketing noise.</summary>
    private static readonly HashSet<string> NoiseWords =
    [
        "pokemon", "tcg", "pkm", "jcc", "cartas", "carta", "card", "cards", "game", "trading", "jogo", "the", "of", "and", "e", "de", "da", "do", "a", "o",
        "mega", "evolution", "evolutions", "scarlet", "violet", "sv", "me", "en", "eng", "english", "ingles", "versao", "edition", "edicao",
        "max", "per", "por", "pessoa", "client", "cliente", "costumer", "customer", "unidade", "unidades", "limit", "limite",
        "reserva", "envio", "selado", "sealed", "novo", "new", "oficial", "official",
        "varios", "vario", "random", "aleatorio", "aleatoria", "sortido", "sortida", "assorted", "x",
        "novidade", "exclusivo", "exclusive", "oferta", "promo", "promocao", "limitada", "limited", "gratis", "portes", "stock",
        "league", "legends", "product", "products", "produto", "produtos", "unidad", "mazo", "inicial", "espanol",
    ];

    /// <summary>Gets every kind, most specific first, ending with <see cref="Other"/>.</summary>
    public static IReadOnlyList<ProductKind> Kinds { get; } =
        [.. Rules.Select(static rule => rule.Kind).DistinctBy(static kind => kind.Code), Other];

    /// <summary>
    /// Classifies one title.
    /// </summary>
    /// <param name="title">The product title as the store shows it.</param>
    /// <returns>The kind; <see cref="Other"/> when nothing matches.</returns>
    public static ProductKind Classify(string title)
    {
        var normalized = Pad(title);
        foreach (var (kind, matches) in Rules)
        {
            if (matches(normalized))
            {
                return kind;
            }
        }

        return Other;
    }

    /// <summary>
    /// The label of a kind code ("etb" → "Elite Trainer Box").
    /// </summary>
    /// <param name="code">The kind code.</param>
    /// <returns>The label, or the <see cref="Other"/> label for an unknown code.</returns>
    public static string LabelOf(string code) =>
        Rules.Select(static rule => rule.Kind).FirstOrDefault(kind => kind.Code.Equals(code, StringComparison.OrdinalIgnoreCase))?.Label ?? Other.Label;

    /// <summary>
    /// The words that tell two products of the same kind apart: the title without the set name, the kind's own words,
    /// numbers and store noise. Usually empty for a set task ("Pitch Black Elite Trainer Box" → ""), a Pokémon for a
    /// variant ("… Checklane Blister - Luxray" → "luxray"), and the set for a task that is not about one set
    /// ("Stellar Crown Elite Trainer Box" → "crown stellar").
    /// </summary>
    /// <param name="title">The product title.</param>
    /// <param name="kind">Its kind.</param>
    /// <param name="setWords">Words of the set name to drop (e.g. "pitch black").</param>
    /// <returns>The variant words, sorted and space-separated.</returns>
    public static string Variant(string title, ProductKind kind, IEnumerable<string> setWords) =>
        DeckCode(title) is { } code
            ? $"st{code}"
            : string.Join(' ', VariantWords(title, kind, setWords).Select(static word => word.Normalized).Order(StringComparer.Ordinal));

    /// <summary>
    /// The same words as <see cref="Variant"/>, in the title's order and casing, for display ("Stellar Crown", "Luxray").
    /// </summary>
    /// <param name="title">The product title.</param>
    /// <param name="kind">Its kind.</param>
    /// <param name="setWords">Words of the set name to drop.</param>
    /// <returns>The variant as written in the title.</returns>
    public static string VariantLabel(string title, ProductKind kind, IEnumerable<string> setWords) =>
        DeckCode(title) is { } code
            ? $"ST-{code:00}"
            : string.Join(' ', VariantWords(title, kind, setWords).Select(static word => word.Original));

    /// <summary>
    /// The number of a starter deck code ("ST-01", "ST01", "st 1" → 1): the code alone identifies the deck, however the
    /// store words the rest of the title.
    /// </summary>
    /// <param name="title">The product title.</param>
    /// <returns>The deck number, or null.</returns>
    public static int? DeckCode(string title) =>
        DeckCodePattern().Match(title) is { Success: true } match && int.TryParse(match.Groups[1].Value, out var number) && number > 0
            ? number
            : null;

    private static List<(string Original, string Normalized)> VariantWords(string title, ProductKind kind, IEnumerable<string> setWords)
    {
        var drop = new HashSet<string>(NoiseWords);
        drop.UnionWith(setWords.SelectMany(TextNormalizer.Words));
        drop.UnionWith(TextNormalizer.Words(kind.Label));
        drop.UnionWith(["booster", "boosters", "pack", "packs", "blister", "box", "tin", "lata", "collection", "checklane", "enhanced", "deck", "ex", "display"]);

        // "Pre-Order" / "Pré-venda" are status, not identity: drop them as phrases so "order" and "venda" stay real words
        // ("Perfect Order").
        var withoutStatus = PreOrderPhrase().Replace(title, " ");
        var seen = new HashSet<string>();
        return WordSplitter().Split(withoutStatus)
            .Where(static token => token.Length > 0)
            .Select(static token => (Original: token, Normalized: TextNormalizer.Normalize(token)))
            .Where(word => !drop.Contains(word.Normalized) && !word.Normalized.All(char.IsDigit) && !IsSeriesCode(word.Normalized) && seen.Add(word.Normalized))
            .ToList();
    }

    [GeneratedRegex(@"(?i)\b(pr[eé][\s-]?(order|venda|venta|sale|reserva)|preorder|prevenda)\b")]
    private static partial Regex PreOrderPhrase();

    [GeneratedRegex(@"[^\p{L}\p{Nd}]+")]
    private static partial Regex WordSplitter();

    [GeneratedRegex(@"(?i)(?<![\p{L}\p{Nd}])st[\s\-_]?0*(\d{1,3})(?![\p{L}\p{Nd}])")]
    private static partial Regex DeckCodePattern();

    private static bool IsSeriesCode(string word) =>
        word.Length <= 5 && (word.StartsWith("me", StringComparison.Ordinal) || word.StartsWith("sv", StringComparison.Ordinal)) && word.Skip(2).All(char.IsDigit);

    private static string Pad(string title) => $" {string.Join(' ', TextNormalizer.Words(title.Replace("&", " & ")))} ";

    private static bool Has(string paddedTitle, string phrase) =>
        paddedTitle.Contains($" {string.Join(' ', TextNormalizer.Words(phrase))} ", StringComparison.Ordinal);

    private static bool HasAny(string paddedTitle, params string[] phrases) => phrases.Any(phrase => Has(paddedTitle, phrase));
}
