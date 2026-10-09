using System.Diagnostics;
using TcgStockBot.Library.Enums;
using TcgStockBot.Library.Text;
using TcgStockBot.WebApi.Extensions.Globalization;
using TcgStockBot.WebApi.Features.Alerts.Models;
using TcgStockBot.WebApi.Features.Alerts.Services;
using TcgStockBot.WebApi.Features.Msrps.Services;
using TcgStockBot.WebApi.Features.Monitors.Models.DTOs;
using TcgStockBot.WebApi.Features.Notifications.Services;
using TcgStockBot.WebApi.Features.PriceHistories.Models.Entities;
using TcgStockBot.WebApi.Features.PriceHistories.Services;
using TcgStockBot.WebApi.Features.Settings.Repositories;
using TcgStockBot.WebApi.Features.StockTasks.Models.Entities;
using TcgStockBot.WebApi.Features.StockTasks.Repositories;
using TcgStockBot.WebApi.Features.StockTasks.Services;
using TcgStockBot.WebApi.Features.Stores.Models.Entities;
using TcgStockBot.WebApi.Features.Stores.Repositories;
using TcgStockBot.WebApi.Features.Stores.Services;
using TcgStockBot.WebApi.Features.Stores.Services.Adapters;

namespace TcgStockBot.WebApi.Features.Monitors.Services;

/// <summary>
/// One full pass: search every running task on each of its stores, then alert on new matches.
/// </summary>
internal interface IStockCheckService
{
    /// <summary>
    /// Runs one check cycle.
    /// </summary>
    /// <param name="cancellationToken">Stops the cycle when the app shuts down.</param>
    /// <returns>The new alerts sent.</returns>
    Task<IReadOnlyList<StockAlert>> RunCycleAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Default <see cref="IStockCheckService"/>.
/// </summary>
/// <param name="stockTaskRepository">Tasks.</param>
/// <param name="storeRepository">Stores.</param>
/// <param name="settingRepository">Settings (pause between requests).</param>
/// <param name="storeAdapterResolver">Platform adapters.</param>
/// <param name="alertService">New-alert selection and history.</param>
/// <param name="notificationService">Alert delivery.</param>
/// <param name="priceHistoryService">Price history of the relevant products.</param>
/// <param name="msrpService">MSRP reference prices (shown in the alerts).</param>
/// <param name="storeHealthService">Failed-cycle counting and automatic turn-off of stores.</param>
/// <param name="activity">Dashboard activity record.</param>
/// <param name="globalizationHelper">Translations.</param>
/// <param name="timeProvider">Clock.</param>
/// <param name="logger">Logger.</param>
internal sealed class StockCheckService(
    IStockTaskRepository stockTaskRepository,
    IStoreRepository storeRepository,
    ISettingRepository settingRepository,
    IStoreAdapterResolver storeAdapterResolver,
    IAlertService alertService,
    INotificationService notificationService,
    IPriceHistoryService priceHistoryService,
    IMsrpService msrpService,
    IStoreHealthService storeHealthService,
    BotActivity activity,
    IGlobalizationHelper globalizationHelper,
    TimeProvider timeProvider,
    ILogger<StockCheckService> logger) : IStockCheckService
{
    private int _cycleNumber;

    /// <inheritdoc />
    public async Task<IReadOnlyList<StockAlert>> RunCycleAsync(CancellationToken cancellationToken)
    {
        var cycleNumber = Interlocked.Increment(ref _cycleNumber);
        var tasks = await stockTaskRepository.ListAsync();
        var stores = await storeRepository.ListAsync();
        var settings = await settingRepository.GetAsync();

        var matches = new List<StockAlert>();
        var completedSearches = new HashSet<string>();
        var failedSearches = 0;
        var lowestPrices = await priceHistoryService.LowestPricesAsync();
        var msrp = await msrpService.LookupAsync();
        var observations = new List<PriceObservation>();
        var storeOutcomes = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        var lastErrors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        activity.CycleStarted();

        // Stores without integration are only a reference in the list: never searched.
        foreach (var store in stores.Where(static store => store.Enabled && store.Platform != StorePlatform.Manual))
        {
            // Rotate the order every cycle: when a domain's rate limit only lets a few requests through,
            // every task still gets its turn instead of the first ones always winning.
            var storeTasks = Rotate(tasks.Where(task => RunsOn(task, store)).ToList(), cycleNumber);

            for (var index = 0; index < storeTasks.Count; index++)
            {
                if (index > 0)
                {
                    await Task.Delay(settings.DelayBetweenRequests, timeProvider, cancellationToken);
                }

                var task = storeTasks[index];
                var report = await SearchAsync(store, task, lowestPrices, cancellationToken);
                activity.SearchCompleted(report);

                if (!report.Succeeded)
                {
                    failedSearches++;
                    if (report.CountsAgainstStore)
                    {
                        storeOutcomes.TryAdd(store.Name, false);   // one search that worked keeps the store healthy
                        lastErrors[store.Name] = report.Error ?? string.Empty;
                    }

                    continue;
                }

                storeOutcomes[store.Name] = true;

                completedSearches.Add(StockAlert.SearchKey(store.Name, task.Id));
                foreach (var listing in report.Listings.Where(static listing => listing.Outcome.IsRelevant() && listing.Price is > 0))
                {
                    observations.Add(new PriceObservation(store.Name, listing.Title, listing.Url, listing.ProductId, listing.Currency, listing.Price!.Value, listing.InStock, task.Id));
                }

                matches.AddRange(report.Listings
                    .Where(static listing => listing.Outcome.IsMatch())
                    .Select(listing => new StockAlert(task.Id, task.Name,
                        new StoreListing(store.Name, listing.Title, new Uri(listing.Url), listing.Price, listing.InStock, listing.ProductId, listing.Currency, listing.Seller, listing.PreOrder),
                        LanguageToConfirm: listing.Outcome == MatchOutcome.MatchLanguageToConfirm,
                        Msrp: msrp.Find(listing.Title, task.Name)?.Price)));
            }
        }

        try
        {
            await priceHistoryService.RecordAsync(observations);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The history is a convenience: never let it stop the alerts.
            if (logger.IsEnabled(LogLevel.Warning))
                logger.LogWarning("Price history not saved: {Error}", exception.Message);
        }

        await RecordStoreHealthAsync(storeOutcomes, lastErrors, settings.AutoDisableAfterFailures, cancellationToken);

        var newAlerts = await alertService.SelectNewAlertsAsync(matches, completedSearches);
        if (newAlerts.Count > 0)
        {
            await alertService.RecordAsync(newAlerts);
            foreach (var failure in await notificationService.NotifyAsync(newAlerts, cancellationToken))
            {
                activity.Log("error", failure);
            }

            foreach (var alert in newAlerts)
            {
                var headline = alert.IsPriceDrop ? $"BAIXOU DE PREÇO ({alert.PreviousPrice:0.00} € →)" : alert.Listing.PreOrder ? "PRÉ-VENDA" : "EM STOCK";
                activity.Log("alert", $"{headline}: {alert.Listing.Title} — {alert.Listing.StoreName} ({alert.Listing.Price:0.00} €)"
                    + (alert.LanguageToConfirm ? " · confirmar idioma" : string.Empty));
            }
        }

        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("Cycle done: {Searches} searches ok, {Failed} failed, {Matches} matching listings, {New} new alert(s)",
                completedSearches.Count, failedSearches, matches.Count, newAlerts.Count);

        activity.CycleFinished(completedSearches.Count + failedSearches, failedSearches, matches.Count, newAlerts.Count);
        return newAlerts;
    }

    /// <summary>
    /// Counts failed cycles per store and turns off the ones that keep failing, with one notice (never blocks the cycle).
    /// </summary>
    private async Task RecordStoreHealthAsync(Dictionary<string, bool> outcomes, Dictionary<string, string> lastErrors, int disableAfterFailures, CancellationToken cancellationToken)
    {
        try
        {
            var turnedOff = await storeHealthService.RecordCycleAsync(outcomes, disableAfterFailures);
            if (turnedOff.Count == 0)
            {
                return;
            }

            var lines = new List<string>();
            foreach (var (name, failures) in turnedOff)
            {
                var line = await globalizationHelper.TranslateAsync(nameof(TranslationKeys.StoreAutoDisabledLine), name, failures, lastErrors.GetValueOrDefault(name, string.Empty));
                lines.Add(line);
                activity.Log("error", $"Loja desligada automaticamente — {line}");
            }

            var subject = await globalizationHelper.TranslateAsync(nameof(TranslationKeys.StoresAutoDisabledSubject), string.Join(", ", turnedOff.Select(static store => store.Name)));
            foreach (var failure in await notificationService.NotifyNoticeAsync(subject, string.Join(Environment.NewLine, lines), cancellationToken))
            {
                activity.Log("error", failure);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            if (logger.IsEnabled(LogLevel.Warning))
                logger.LogWarning("Store health not saved: {Error}", exception.Message);
        }
    }

    private static List<StockTask> Rotate(List<StockTask> tasks, int offset) =>
        tasks.Count <= 1 ? tasks : [.. tasks.Skip(offset % tasks.Count), .. tasks.Take(offset % tasks.Count)];

    private static bool RunsOn(StockTask task, Store store) =>
        task.Enabled && (task.StoreNames.Count == 0 || task.StoreNames.Contains(store.Name, StringComparer.OrdinalIgnoreCase));

    private async Task<SearchReportDTO> SearchAsync(Store store, StockTask task, IReadOnlyDictionary<string, decimal> lowestPrices, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var report = new SearchReportDTO { StoreName = store.Name, StockTaskId = task.Id };

        try
        {
            var listings = await storeAdapterResolver.Resolve(store).SearchAsync(store, task.SearchQuery, cancellationToken);

            report.Succeeded = true;
            report.Listings = listings.Select(listing => new ListingReportDTO
            {
                PriceHistoryKey = PriceObservation.KeyFor(store.Name, listing.Url.AbsoluteUri),
                Title = listing.Title,
                Url = listing.Url.AbsoluteUri,
                Price = listing.Price,
                InStock = listing.InStock,
                Language = LanguageClassifier.Classify(listing.Title),
                Outcome = StockTaskMatcher.Evaluate(task, listing, store.UnmarkedLanguage),
                ProductId = listing.ProductId,
                Currency = listing.Currency,
                Seller = listing.Seller,
                PreOrder = listing.PreOrder || PreOrderDetector.IsPreOrder(listing.Title),
            }).ToList();

            foreach (var listing in report.Listings)
            {
                var seen = lowestPrices.TryGetValue(listing.PriceHistoryKey, out var lowest) ? lowest : (decimal?)null;
                listing.LowestPrice = (seen, listing.Price) switch
                {
                    ({ } history, { } now) => Math.Min(history, now),
                    (null, { } now) => now,
                    ({ } history, null) => history,
                    _ => null,
                };
            }
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            if (logger.IsEnabled(LogLevel.Warning))
                logger.LogWarning("Search failed on {Store} for '{Query}': {Error}", store.Name, task.SearchQuery, exception.Message);

            report.Succeeded = false;
            report.Error = await StoreFailure.DescribeAsync(exception, globalizationHelper);
            report.CountsAgainstStore = StoreHealthService.IsStoreFailure(exception);
        }

        report.CheckedAt = timeProvider.GetUtcNow();
        report.DurationMs = stopwatch.ElapsedMilliseconds;
        return report;
    }
}
