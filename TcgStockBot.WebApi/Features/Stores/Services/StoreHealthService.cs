using System.Diagnostics;
using System.Net;
using TcgStockBot.Library.Enums;
using TcgStockBot.Library.Services;
using TcgStockBot.WebApi.Extensions.Globalization;
using TcgStockBot.WebApi.Extensions.Http;
using TcgStockBot.WebApi.Features.Msrps.Services;
using TcgStockBot.WebApi.Features.Sets.Services;
using TcgStockBot.WebApi.Features.StockTasks.Repositories;
using TcgStockBot.WebApi.Features.StockTasks.Services;
using TcgStockBot.WebApi.Features.Stores.Models.DTOs;
using TcgStockBot.WebApi.Features.Stores.Models.Entities;
using TcgStockBot.WebApi.Features.Stores.Repositories;
using TcgStockBot.WebApi.Features.Stores.Services.Adapters;

namespace TcgStockBot.WebApi.Features.Stores.Services;

/// <summary>
/// Default <see cref="IStoreHealthService"/>.
/// </summary>
/// <param name="storeRepository">Store data access.</param>
/// <param name="stockTaskRepository">Tasks (to count the products they recognise).</param>
/// <param name="storeAdapterResolver">Picks the platform adapter.</param>
/// <param name="globalizationHelper">Translations.</param>
/// <param name="timeProvider">Clock.</param>
internal sealed class StoreHealthService(
    IStoreRepository storeRepository,
    IStockTaskRepository stockTaskRepository,
    IStoreAdapterResolver storeAdapterResolver,
    IGlobalizationHelper globalizationHelper,
    TimeProvider timeProvider,
    IMsrpService? msrpService = null,
    ISetService? setService = null) : IStoreHealthService
{
    /// <summary>The tag added to a store the bot turned off; removed when it is turned on again.</summary>
    internal const string FailingTag = "failing";

    /// <summary>The tag of stores still to test; a test that finds products removes it.</summary>
    internal const string UntestedTag = "untested";

    /// <summary>The same tags as earlier versions wrote them (Portuguese), still removed.</summary>
    internal static readonly string[] LegacyFailingTags = [FailingTag, "a falhar"];

    /// <summary>The untested tag in this and earlier versions.</summary>
    internal static readonly string[] LegacyUntestedTags = [UntestedTag, "por testar"];

    /// <summary>Whether a tag is one of the given ones (case-insensitive).</summary>
    internal static bool IsTag(string tag, string[] tags) => tags.Any(other => other.Equals(tag, StringComparison.OrdinalIgnoreCase));

    /// <summary>The search text of a test when none is given.</summary>
    internal const string DefaultQuery = "pokemon";

    private const string FieldName = "Name";

    /// <summary>A test never waits longer than this (a large Shopify catalog can take a while).</summary>
    private static readonly TimeSpan TestBudget = TimeSpan.FromSeconds(50);

    private readonly IStoreRepository _storeRepository = storeRepository;
    private readonly IStockTaskRepository _stockTaskRepository = stockTaskRepository;
    private readonly IStoreAdapterResolver _storeAdapterResolver = storeAdapterResolver;
    private readonly IGlobalizationHelper _globalizationHelper = globalizationHelper;
    private readonly TimeProvider _timeProvider = timeProvider;

    /// <inheritdoc />
    public async Task<ServiceApiResponse<StoreTestResultDTO>> TestAsync(StoreTestRequest item)
    {
        if (string.IsNullOrWhiteSpace(item.Name))
        {
            return ServiceApiResponse<StoreTestResultDTO>
                .ValidationProblems(await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.ValidationProblems)))
                .AddErrorRange([await _globalizationHelper.ValidationErrorAsync(FieldName, nameof(TranslationKeys.RequiredField))]);
        }

        var store = await _storeRepository.GetAsync(item.Name.Trim());
        if (store is null)
        {
            return ServiceApiResponse<StoreTestResultDTO>.NotFound(await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.NoRecordsFound), item.Name));
        }

        var record = await RunTestAsync(store, string.IsNullOrWhiteSpace(item.Query) ? DefaultQuery : item.Query.Trim());
        await _storeRepository.UpdateAsync(stores =>
        {
            foreach (var saved in stores.Where(saved => saved.Name.Equals(store.Name, StringComparison.OrdinalIgnoreCase)))
            {
                saved.LastTest = record;
                if (record.Status == StoreTestStatus.Ok)
                {
                    saved.Tags.RemoveAll(static tag => IsTag(tag, LegacyUntestedTags));
                }
            }
        });

        return ServiceApiResponse<StoreTestResultDTO>.Success(new StoreTestResultDTO(store.Name, record));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<(string Name, int Failures)>> RecordCycleAsync(IReadOnlyDictionary<string, bool> outcomes, int disableAfterFailures)
    {
        if (outcomes.Count == 0)
        {
            return [];
        }

        var now = _timeProvider.GetUtcNow();
        var turnedOff = new List<(string Name, int Failures)>();

        await _storeRepository.UpdateAsync(stores =>
        {
            turnedOff.Clear();
            foreach (var store in stores)
            {
                if (!outcomes.TryGetValue(store.Name, out var worked))
                {
                    continue;   // not searched this cycle, or only failures that were not the store's fault
                }

                if (worked)
                {
                    store.ConsecutiveFailures = 0;
                    continue;
                }

                store.ConsecutiveFailures++;
                if (disableAfterFailures > 0 && store.Enabled && store.ConsecutiveFailures >= disableAfterFailures)
                {
                    store.Enabled = false;
                    store.AutoDisabledAt = now;
                    if (!store.Tags.Any(static tag => IsTag(tag, LegacyFailingTags)))
                    {
                        store.Tags.Add(FailingTag);
                    }

                    turnedOff.Add((store.Name, store.ConsecutiveFailures));
                }
            }
        });

        return turnedOff;
    }

    /// <summary>
    /// Whether a failed search counts against the store: not when the bot's own per-domain limit postponed it, and not
    /// when the provider is simply not configured yet (Amazon credentials, an Awin feed, a store without integration).
    /// </summary>
    /// <param name="exception">Why the search failed.</param>
    /// <returns>True when the failure is the store's.</returns>
    internal static bool IsStoreFailure(Exception exception) => exception is not (OutboundThrottledException
        or StoreWithoutIntegrationException
        or StoreIntegrationException { Problem: StoreIntegrationProblem.NotConfigured }
        or OperationCanceledException { CancellationToken.IsCancellationRequested: true });

    /// <summary>The test outcome of a failure.</summary>
    /// <param name="exception">Why the search failed.</param>
    /// <returns>The status.</returns>
    internal static StoreTestStatus StatusOf(Exception exception) => exception switch
    {
        StoreWithoutIntegrationException => StoreTestStatus.NoIntegration,
        StoreIntegrationException { Problem: StoreIntegrationProblem.NotConfigured } => StoreTestStatus.NotConfigured,
        OutboundThrottledException => StoreTestStatus.RateLimited,
        HttpRequestException { StatusCode: HttpStatusCode.Forbidden } => StoreTestStatus.Blocked,
        HttpRequestException { StatusCode: HttpStatusCode.TooManyRequests } => StoreTestStatus.RateLimited,
        HttpRequestException { StatusCode: null } => StoreTestStatus.Unreachable,
        TaskCanceledException => StoreTestStatus.Unreachable,
        _ => StoreTestStatus.Error,
    };

    private async Task<StoreTestRecord> RunTestAsync(Store store, string query)
    {
        var stopwatch = Stopwatch.StartNew();
        var record = new StoreTestRecord { At = _timeProvider.GetUtcNow(), Query = query };

        try
        {
            using var timeout = new CancellationTokenSource(TestBudget);
            var listings = await _storeAdapterResolver.Resolve(store).SearchAsync(store, query, timeout.Token);
            var tasks = await _stockTaskRepository.ListAsync();
            var msrp = msrpService is null ? MsrpLookup.Empty : await msrpService.LookupAsync();
            var sets = setService is null ? null : await setService.LookupAsync();
            var outcomes = listings.Select(listing => tasks.Select(task => StockTaskMatcher.Evaluate(task, listing, store.UnmarkedLanguage, msrp, sets)).ToList()).ToList();

            record.Status = listings.Count > 0 ? StoreTestStatus.Ok : StoreTestStatus.Empty;
            record.Results = listings.Count;
            record.Relevant = outcomes.Count(static perTask => perTask.Any(static outcome => outcome.IsRelevant()));
            record.Matches = outcomes.Count(static perTask => perTask.Any(static outcome => outcome.IsMatch()));
            record.Sample = listings.Count > 0 ? listings[0].Title : string.Empty;
        }
        catch (Exception exception)
        {
            // Any failure is reported, never a 500: the dashboard shows it next to the store.
            record.Status = StatusOf(exception);
            record.Message = await StoreFailure.DescribeAsync(exception, _globalizationHelper);
        }

        record.DurationMs = stopwatch.ElapsedMilliseconds;
        return record;
    }
}
