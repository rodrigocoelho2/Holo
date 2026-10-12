using System.Globalization;
using TcgStockBot.Library.Enums;
using TcgStockBot.Library.Services;
using TcgStockBot.Library.Text;
using TcgStockBot.Library.Validation;
using TcgStockBot.WebApi.Extensions.Globalization;
using TcgStockBot.WebApi.Features.Compares.Models.DTOs;
using TcgStockBot.WebApi.Features.Compares.Repositories;
using TcgStockBot.WebApi.Features.Msrps.Services;
using TcgStockBot.WebApi.Features.PriceHistories.Models.DTOs;
using TcgStockBot.WebApi.Features.PriceHistories.Models.Entities;
using TcgStockBot.WebApi.Features.PriceHistories.Services;
using TcgStockBot.WebApi.Features.Sets.Models.Entities;
using TcgStockBot.WebApi.Features.Sets.Services;
using TcgStockBot.WebApi.Features.StockTasks.Models.Entities;
using TcgStockBot.WebApi.Features.StockTasks.Repositories;

namespace TcgStockBot.WebApi.Features.Compares.Services;

/// <summary>
/// Groups the price-history listings into products: automatically by game + set + kind of product
/// (<see cref="ProductKindClassifier"/>), unless a manual correction in compare-overrides.json says otherwise. The set is
/// the one the title names (<see cref="SetLookup"/>), else the one the task is about; a listing of an unknown set is
/// grouped by its task. Listings whose task no longer exists (or that were recorded before tasks were remembered) are
/// left out.
/// </summary>
/// <param name="priceHistoryService">The price histories.</param>
/// <param name="stockTaskRepository">The tasks (set names).</param>
/// <param name="compareOverrideRepository">Manual corrections.</param>
/// <param name="msrpService">MSRP reference prices.</param>
/// <param name="setService">The sets of every game.</param>
/// <param name="globalizationHelper">Translations.</param>
internal sealed class CompareService(
    IPriceHistoryService priceHistoryService,
    IStockTaskRepository stockTaskRepository,
    ICompareOverrideRepository compareOverrideRepository,
    IMsrpService msrpService,
    ISetService setService,
    IGlobalizationHelper globalizationHelper) : ICompareService
{
    /// <summary>The CompareAssign target that makes a listing a product of its own.</summary>
    internal const string Solo = "solo";

    private const string FieldHistoryKey = "HistoryKey";
    private const string FieldGroupKey = "GroupKey";
    private const string FieldIdentifier = "Identifier";
    private const string SoloPrefix = "solo|";
    private const string SetPrefix = "set:";

    /// <summary>
    /// Kinds where one set has several different products (a blister per Pokémon, a deck per leader): the leftover words
    /// of the title tell them apart. For every other kind the set alone is the product, so store wording never splits it.
    /// </summary>
    private static readonly HashSet<string> KindsWithVariants = new(StringComparer.OrdinalIgnoreCase)
    {
        "checklane-blister", "blister-2", "blister-3", "tin", "ex-box", "premium-collection", "collection", "deck", "commander-deck", "other",
    };

    private static readonly CultureInfo Portuguese = CultureInfo.GetCultureInfo("pt-PT");

    private readonly IPriceHistoryService _priceHistoryService = priceHistoryService;
    private readonly IStockTaskRepository _stockTaskRepository = stockTaskRepository;
    private readonly ICompareOverrideRepository _compareOverrideRepository = compareOverrideRepository;
    private readonly IMsrpService _msrpService = msrpService;
    private readonly ISetService _setService = setService;
    private readonly IGlobalizationHelper _globalizationHelper = globalizationHelper;

    /// <inheritdoc />
    public async Task<ServiceApiResponse<List<CompareGroupDTO>>> ListAsync() =>
        ServiceApiResponse<List<CompareGroupDTO>>.Success(await BuildGroupsAsync(withPoints: false));

    /// <inheritdoc />
    public async Task<ServiceApiResponse<CompareGroupDTO>> GetAsync(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            return (await ValidationProblemsAsync<CompareGroupDTO>())
                .AddErrorRange([await _globalizationHelper.ValidationErrorAsync(FieldIdentifier, nameof(TranslationKeys.RequiredField))]);
        }

        var group = (await BuildGroupsAsync(withPoints: true)).FirstOrDefault(group => group.Key.Equals(identifier.Trim(), StringComparison.OrdinalIgnoreCase));
        return group is null
            ? ServiceApiResponse<CompareGroupDTO>.NotFound(await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.NoRecordsFound), identifier))
            : ServiceApiResponse<CompareGroupDTO>.Success(group);
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<string>> AssignAsync(CompareAssignRequest item)
    {
        var entries = await _priceHistoryService.SnapshotAsync();
        var tasks = await TasksAsync();
        var sets = await _setService.LookupAsync();
        var historyKey = (item.HistoryKey ?? string.Empty).Trim().ToLowerInvariant();
        var groupKey = (item.GroupKey ?? string.Empty).Trim();

        var validationErrors = await ValidateAssignAsync(historyKey, groupKey, entries, tasks, sets);
        if (validationErrors.Count > 0)
        {
            return (await ValidationProblemsAsync<string>()).AddErrorRange(validationErrors);
        }

        var overrides = await _compareOverrideRepository.ListAsync();
        var automatic = AutomaticGroupKey(entries[historyKey], tasks, sets);

        if (groupKey.Length == 0 || groupKey.Equals(automatic, StringComparison.OrdinalIgnoreCase))
        {
            overrides.Remove(historyKey);   // back to (or already in) the automatic group: nothing to remember
        }
        else
        {
            overrides[historyKey] = groupKey.Equals(Solo, StringComparison.OrdinalIgnoreCase) ? Solo : groupKey;
        }

        await _compareOverrideRepository.SaveAsync(overrides);
        return ServiceApiResponse<string>.Success();
    }

    /// <summary>
    /// The automatic group of a listing: "set:{set}|{kind}|{variant}" when its set is known (the variant only for kinds
    /// with variants), else "{task}|{kind}|{variant}" for its first existing task.
    /// </summary>
    /// <param name="entry">The listing's price history.</param>
    /// <param name="tasks">The tasks by id.</param>
    /// <param name="sets">The sets.</param>
    /// <returns>The group key, or null when no task of the listing exists any more.</returns>
    internal static string? AutomaticGroupKey(PriceHistoryEntry entry, IReadOnlyDictionary<string, StockTask> tasks, SetLookup sets)
    {
        var task = FirstTask(entry, tasks);
        if (task is null)
        {
            return null;
        }

        var kind = ProductKindClassifier.Classify(entry.Title);
        var set = sets.Find(task.Game, entry.Title) ?? sets.ForTask(task);
        if (set is not null)
        {
            var setVariant = KindsWithVariants.Contains(kind.Code) ? ProductKindClassifier.Variant(entry.Title, kind, SetWords(set, task)) : string.Empty;
            return $"{SetPrefix}{set.Id}|{kind.Code}|{setVariant}".ToLowerInvariant();
        }

        var variant = ProductKindClassifier.Variant(entry.Title, kind, SetWords(task));
        return $"{task.Id}|{kind.Code}|{variant}".ToLowerInvariant();
    }

    private static StockTask? FirstTask(PriceHistoryEntry entry, IReadOnlyDictionary<string, StockTask> tasks) =>
        entry.StockTaskIds.Select(id => tasks.GetValueOrDefault(id)).FirstOrDefault(static task => task is not null);

    private async Task<List<CompareGroupDTO>> BuildGroupsAsync(bool withPoints)
    {
        var entries = await _priceHistoryService.SnapshotAsync();
        var tasks = await TasksAsync();
        var sets = await _setService.LookupAsync();
        var overrides = await _compareOverrideRepository.ListAsync();
        var msrp = await _msrpService.LookupAsync();
        var groups = new Dictionary<string, CompareGroupDTO>(StringComparer.OrdinalIgnoreCase);

        foreach (var (historyKey, entry) in entries)
        {
            if (entry.Points.Count == 0)
            {
                continue;
            }

            var isManual = overrides.TryGetValue(historyKey, out var overridden);
            var groupKey = isManual
                ? overridden!.Equals(Solo, StringComparison.OrdinalIgnoreCase) ? SoloPrefix + historyKey : overridden
                : AutomaticGroupKey(entry, tasks, sets);

            if (groupKey is null)
            {
                continue;
            }

            if (!groups.TryGetValue(groupKey, out var group))
            {
                group = NewGroup(groupKey, entry, tasks, sets);
                SetMsrp(group, msrp, entry, tasks);
                groups[groupKey] = group;
            }

            group.Listings.Add(Listing(historyKey, entry, isManual, withPoints));
        }

        foreach (var group in groups.Values)
        {
            group.Listings = [.. group.Listings.OrderByDescending(static listing => listing.CurrentInStock).ThenBy(static listing => listing.CurrentPrice)];
        }

        return [.. groups.Values.OrderBy(static group => group.Title, StringComparer.CurrentCultureIgnoreCase)];
    }

    /// <summary>Names a group from its key: "set · kind – variant", or the listing's own title for a separated listing.</summary>
    private static CompareGroupDTO NewGroup(string groupKey, PriceHistoryEntry entry, IReadOnlyDictionary<string, StockTask> tasks, SetLookup sets)
    {
        if (groupKey.StartsWith(SoloPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var soloKind = ProductKindClassifier.Classify(entry.Title);
            var soloTask = entry.StockTaskIds.Select(id => tasks.GetValueOrDefault(id)).FirstOrDefault(static task => task is not null);
            return new CompareGroupDTO
            {
                Key = groupKey, Title = entry.Title, SetName = string.Empty, KindLabel = soloKind.Label, Kind = soloKind.Code,
                Game = soloTask?.Game ?? TcgGame.Pokemon, StockTaskId = soloTask?.Id,
            };
        }

        var parts = groupKey.Split('|');
        if (parts[0].StartsWith(SetPrefix, StringComparison.OrdinalIgnoreCase) && sets.Get(parts[0][SetPrefix.Length..]) is { } set)
        {
            return SetGroup(groupKey, parts, set, entry, tasks);
        }

        var task = parts.Length > 0 ? tasks.GetValueOrDefault(parts[0]) : null;
        var setName = task is null ? parts[0] : SetName(task);
        var kindLabel = parts.Length > 1 ? ProductKindClassifier.LabelOf(parts[1]) : ProductKindClassifier.Other.Label;
        var variantLabel = parts.Length > 2 && parts[2].Length > 0 && task is not null
            ? ProductKindClassifier.VariantLabel(entry.Title, ProductKindClassifier.Classify(entry.Title), SetWords(task))
            : string.Empty;
        var variant = variantLabel.Length > 0 ? $" – {variantLabel}" : parts.Length > 2 && parts[2].Length > 0 ? $" – {Portuguese.TextInfo.ToTitleCase(parts[2])}" : string.Empty;

        return new CompareGroupDTO
        {
            Key = groupKey, Title = $"{setName} · {kindLabel}{variant}", SetName = setName, KindLabel = kindLabel,
            Kind = parts.Length > 1 ? parts[1] : ProductKindClassifier.Other.Code, Game = task?.Game ?? TcgGame.Pokemon, StockTaskId = task?.Id,
            VariantLabel = variant.Length > 0 ? variant[3..] : string.Empty,
        };
    }

    /// <summary>A group of a known set: "Pitch Black · Elite Trainer Box", with the set's code and release date.</summary>
    private static CompareGroupDTO SetGroup(string groupKey, string[] parts, TcgSet set, PriceHistoryEntry entry, IReadOnlyDictionary<string, StockTask> tasks)
    {
        var task = FirstTask(entry, tasks);
        var kindCode = parts.Length > 1 && parts[1].Length > 0 ? parts[1] : ProductKindClassifier.Other.Code;
        var kindLabel = ProductKindClassifier.LabelOf(kindCode);
        var variantLabel = parts.Length > 2 && parts[2].Length > 0
            ? ProductKindClassifier.VariantLabel(entry.Title, ProductKindClassifier.Classify(entry.Title), task is null ? SetLookup.SetWords(set) : SetWords(set, task))
            : string.Empty;
        if (variantLabel.Length == 0 && parts.Length > 2 && parts[2].Length > 0)
        {
            variantLabel = Portuguese.TextInfo.ToTitleCase(parts[2]);
        }

        return new CompareGroupDTO
        {
            Key = groupKey,
            Title = $"{set.Name} · {kindLabel}{(variantLabel.Length > 0 ? $" – {variantLabel}" : string.Empty)}",
            SetName = set.Name,
            SetId = set.Id,
            SetCode = set.Code,
            ReleaseDate = set.ReleaseDate,
            KindLabel = kindLabel,
            Kind = kindCode,
            VariantLabel = variantLabel,
            Game = set.Game,
            StockTaskId = task?.Id,
        };
    }

    /// <summary>The group's reference price, from its first listing's title and the set (task) it belongs to.</summary>
    private static void SetMsrp(CompareGroupDTO group, MsrpLookup msrp, PriceHistoryEntry entry, IReadOnlyDictionary<string, StockTask> tasks)
    {
        var taskId = group.Key.StartsWith(SoloPrefix, StringComparison.OrdinalIgnoreCase) || group.Key.StartsWith(SetPrefix, StringComparison.OrdinalIgnoreCase)
            ? null
            : group.Key.Split('|')[0];
        var task = taskId is null ? entry.StockTaskIds.Select(id => tasks.GetValueOrDefault(id)).FirstOrDefault(static task => task is not null) : tasks.GetValueOrDefault(taskId);
        var reference = msrp.Find(entry.Title, task?.Name);
        if (reference is null)
        {
            return;
        }

        group.Msrp = reference.Price;
        group.MsrpBasis = reference.Basis;
        group.MsrpLabel = $"{reference.Game} · {ProductKindClassifier.LabelOf(reference.Kind)}";
    }

    /// <summary>The price in force at a moment (the last change before it), or the first price when the listing is newer.</summary>
    internal static decimal? PriceAt(PriceHistoryEntry entry, DateTimeOffset moment) =>
        entry.Points.LastOrDefault(point => point.At <= moment)?.Price ?? entry.Points.FirstOrDefault()?.Price;

    private static CompareListingDTO Listing(string historyKey, PriceHistoryEntry entry, bool isManual, bool withPoints)
    {
        var current = entry.Points[^1];
        var lowest = entry.Points.MinBy(static point => point.Price)!;
        var listing = new CompareListingDTO
        {
            HistoryKey = historyKey,
            StoreName = entry.StoreName,
            Title = entry.Title,
            Url = entry.Url,
            CurrentPrice = current.Price,
            CurrentInStock = current.InStock,
            LowestPrice = lowest.Price,
            LowestPriceAt = lowest.At,
            LastSeen = entry.LastSeen,
            IsManual = isManual,
            FirstSeen = entry.FirstSeen,
            ImageUrl = entry.ImageUrl,
            Price30DaysAgo = PriceAt(entry, entry.LastSeen.AddDays(-30)),
            Points = withPoints ? entry.Points.Select(static point => new PricePointDTO(point)).ToList() : [],
        };

        for (var index = entry.Points.Count - 1; index > 0; index--)
        {
            if (entry.Points[index].Price < entry.Points[index - 1].Price)
            {
                listing.LastDropFrom = entry.Points[index - 1].Price;
                listing.LastDropTo = entry.Points[index].Price;
                listing.LastDropAt = entry.Points[index].At;
                break;
            }
        }

        return listing;
    }

    /// <summary>"Pokémon Pitch Black (EN)" → "Pitch Black"; the language is kept only when it is not English.</summary>
    private static string SetName(StockTask task)
    {
        var name = task.Name.Trim();
        foreach (var prefix in new[] { "Pokémon ", "Pokemon " })
        {
            if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                name = name[prefix.Length..];
            }
        }

        return name.Replace("(EN)", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("— tudo", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim(' ', '—', '-');
    }

    /// <summary>The words of the set, to drop from variant names: the task's required keywords and its set name.</summary>
    private static IEnumerable<string> SetWords(StockTask task) =>
        task.RequiredKeywords
            .SelectMany(static keyword => keyword.Split('|'))
            .Select(static alternative => alternative.Trim().TrimEnd('*'))
            .Append(SetName(task));

    /// <summary>The words to drop from the variant of a known set: its name, code and aliases, and the task's words.</summary>
    private static IEnumerable<string> SetWords(TcgSet set, StockTask task) => SetLookup.SetWords(set).Concat(SetWords(task));

    private async Task<Dictionary<string, StockTask>> TasksAsync() =>
        (await _stockTaskRepository.ListAsync()).ToDictionary(static task => task.Id, StringComparer.OrdinalIgnoreCase);

    private async Task<List<ApiValidationError>> ValidateAssignAsync(
        string historyKey, string groupKey, IReadOnlyDictionary<string, PriceHistoryEntry> entries, IReadOnlyDictionary<string, StockTask> tasks, SetLookup sets)
    {
        var validationErrors = new List<ApiValidationError>();

        if (historyKey.Length == 0)
        {
            validationErrors.Add(await _globalizationHelper.ValidationErrorAsync(FieldHistoryKey, nameof(TranslationKeys.RequiredField)));
        }
        else if (!entries.ContainsKey(historyKey))
        {
            validationErrors.Add(new ApiValidationError(FieldHistoryKey, await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.NoRecordsFound), historyKey)));
        }

        // A target group is "solo", empty, a separated listing's group, "set:id|kind|variant" of a known set or
        // "task|kind|variant" of an existing task.
        var isGroup = groupKey.Length == 0
            || groupKey.Equals(Solo, StringComparison.OrdinalIgnoreCase)
            || (groupKey.StartsWith(SoloPrefix, StringComparison.OrdinalIgnoreCase) && entries.ContainsKey(groupKey[SoloPrefix.Length..]))
            || (groupKey.Split('|') is { Length: 3 } parts
                && (tasks.ContainsKey(parts[0])
                    || (parts[0].StartsWith(SetPrefix, StringComparison.OrdinalIgnoreCase) && sets.Get(parts[0][SetPrefix.Length..]) is not null)));
        if (!isGroup)
        {
            validationErrors.Add(new ApiValidationError(FieldGroupKey, await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.NoRecordsFound), groupKey)));
        }

        return validationErrors;
    }

    private async Task<ServiceApiResponse<T>> ValidationProblemsAsync<T>() =>
        ServiceApiResponse<T>.ValidationProblems(await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.ValidationProblems)));
}
