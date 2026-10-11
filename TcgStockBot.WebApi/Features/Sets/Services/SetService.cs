using System.Text.RegularExpressions;
using TcgStockBot.Library.Text;
using TcgStockBot.WebApi.Features.Releases.Repositories;
using TcgStockBot.WebApi.Features.Sets.Models.Entities;
using TcgStockBot.WebApi.Features.Sets.Repositories;

namespace TcgStockBot.WebApi.Features.Sets.Services;

/// <summary>
/// Merges the built-in sets with the releases: a release with the name (or an alias) of a built-in set of the same
/// game adds its aliases, date and watched task to it; any other release is a set of its own. The game in front of a
/// release name ("Pokémon — Delta Reign") is not part of the set name.
/// </summary>
/// <param name="setRepository">The built-in sets.</param>
/// <param name="releaseRepository">The releases.</param>
internal sealed partial class SetService(ISetRepository setRepository, IReleaseRepository releaseRepository) : ISetService
{
    /// <summary>Prefix of the ids of sets that come from a release.</summary>
    internal const string ReleasePrefix = "rel-";

    private readonly ISetRepository _setRepository = setRepository;
    private readonly IReleaseRepository _releaseRepository = releaseRepository;

    /// <inheritdoc />
    public async Task<SetLookup> LookupAsync()
    {
        var sets = await _setRepository.ListAsync();
        var builtIn = new SetLookup(sets);
        var taskSets = new Dictionary<string, TcgSet>(StringComparer.OrdinalIgnoreCase);

        foreach (var release in await _releaseRepository.ListAsync())
        {
            var name = SetName(release.Name);
            if (name.Length == 0)
            {
                continue;
            }

            // The same set: a name or alias that is a built-in set's phrase, or names one ("Pokémon — Pitch Black").
            var phrases = new[] { name }.Concat(release.Aliases).Select(Phrase).Where(static phrase => phrase.Length > 0).ToList();
            var set = sets.FirstOrDefault(set => set.Game == release.Game && !set.Id.StartsWith(ReleasePrefix, StringComparison.Ordinal)
                    && SetLookup.Phrases(set).Any(phrases.Contains))
                ?? (builtIn.Find(release.Game, release.Name) is { Series: false } named ? named : null);

            if (set is null)
            {
                set = new TcgSet { Id = ReleasePrefix + release.Id, Name = name, Game = release.Game };
                sets.Add(set);
            }

            set.ReleaseDate = release.ReleaseDate;
            set.Aliases = [.. set.Aliases.Concat(release.Aliases).Append(name).Where(static alias => !string.IsNullOrWhiteSpace(alias))
                .DistinctBy(Phrase)];

            if (!string.IsNullOrWhiteSpace(release.StockTaskId))
            {
                taskSets[release.StockTaskId] = set;
            }
        }

        return new SetLookup(sets, taskSets);
    }

    /// <summary>"Pokémon — Delta Reign" → "Delta Reign"; "One Piece OP-15" → "OP-15".</summary>
    internal static string SetName(string releaseName) => GamePrefix().Replace(releaseName.Trim(), string.Empty).Trim();

    private static string Phrase(string text) => string.Join(' ', TextNormalizer.Words(text));

    [GeneratedRegex(@"^(pok[eé]mon( tcg)?|one piece( card game| tcg)?|riftbound|cyberpunk( tcg)?|lorcana|magic( the gathering)?|mtg)\s*[—–:\-]?\s*", RegexOptions.IgnoreCase)]
    private static partial Regex GamePrefix();
}
