using TcgStockBot.Library.Enums;
using TcgStockBot.Library.Text;
using TcgStockBot.WebApi.Features.Sets.Models.Entities;
using TcgStockBot.WebApi.Features.StockTasks.Models.Entities;

namespace TcgStockBot.WebApi.Features.Sets.Services;

/// <summary>
/// Finds the set a product title names: its name, code ("OP-13", "op13") or an alias, as whole words. When several
/// sets are named the longest name wins ("Mega Evolution 5 - Pitch Black" is Pitch Black), and a series name
/// ("Mega Evolution") only counts when nothing else is named.
/// </summary>
internal sealed class SetLookup
{
    private readonly Dictionary<string, TcgSet> _byId;
    private readonly ILookup<TcgGame, (TcgSet Set, string Phrase)> _phrases;
    private readonly IReadOnlyDictionary<string, TcgSet> _byTask;

    /// <summary>
    /// Initializes a new instance of the <see cref="SetLookup"/> class.
    /// </summary>
    /// <param name="sets">The sets.</param>
    /// <param name="byTask">The set each task watches (from the releases), by task id.</param>
    public SetLookup(IEnumerable<TcgSet> sets, IReadOnlyDictionary<string, TcgSet>? byTask = null)
    {
        var list = sets.Where(static set => set.Id.Length > 0).ToList();
        _byId = list.DistinctBy(static set => set.Id, StringComparer.OrdinalIgnoreCase).ToDictionary(static set => set.Id, StringComparer.OrdinalIgnoreCase);
        _phrases = list.SelectMany(static set => Phrases(set).Select(phrase => (set, phrase))).ToLookup(static item => item.set.Game);
        _byTask = byTask ?? new Dictionary<string, TcgSet>();
    }

    /// <summary>Gets an empty lookup.</summary>
    public static SetLookup Empty { get; } = new([]);

    /// <summary>
    /// Gets a set by id.
    /// </summary>
    /// <param name="id">The set id.</param>
    /// <returns>The set, or null.</returns>
    public TcgSet? Get(string id) => _byId.GetValueOrDefault(id);

    /// <summary>
    /// The set a title names.
    /// </summary>
    /// <param name="game">The game of the product.</param>
    /// <param name="title">The product title.</param>
    /// <returns>The set, or null when the title names none.</returns>
    public TcgSet? Find(TcgGame game, string title)
    {
        var padded = $" {string.Join(' ', TextNormalizer.Words(title))} ";
        return _phrases[game]
            .Where(item => padded.Contains($" {item.Phrase} ", StringComparison.Ordinal))
            .OrderBy(static item => item.Set.Series)
            .ThenByDescending(static item => item.Phrase.Length)
            .ThenByDescending(static item => item.Set.ReleaseDate)
            .Select(static item => item.Set)
            .FirstOrDefault();
    }

    /// <summary>
    /// The set a task is about: the one a release links to it, else the one its name names.
    /// </summary>
    /// <param name="task">The task.</param>
    /// <returns>The set, or null for a task that is not about one set ("Pokémon Elite Trainer Box").</returns>
    public TcgSet? ForTask(StockTask task) =>
        _byTask.GetValueOrDefault(task.Id) ?? Find(task.Game, task.Name);

    /// <summary>
    /// The normalized phrases a set is recognised by: name, code (with and without the space: "op 13", "op13") and aliases.
    /// </summary>
    /// <param name="set">The set.</param>
    /// <returns>The phrases.</returns>
    internal static IEnumerable<string> Phrases(TcgSet set)
    {
        var phrases = new List<string> { Words(set.Name) };
        if (!string.IsNullOrWhiteSpace(set.Code))
        {
            var code = Words(set.Code);
            phrases.Add(code);
            phrases.Add(code.Replace(" ", string.Empty, StringComparison.Ordinal));
        }

        phrases.AddRange(set.Aliases.Select(Words));
        return phrases.Where(static phrase => phrase.Length > 0).Distinct();
    }

    /// <summary>
    /// The words of a set to leave out of a variant name: its name, code and aliases.
    /// </summary>
    /// <param name="set">The set.</param>
    /// <returns>The words.</returns>
    internal static IEnumerable<string> SetWords(TcgSet set) => Phrases(set);

    private static string Words(string text) => string.Join(' ', TextNormalizer.Words(text));
}
