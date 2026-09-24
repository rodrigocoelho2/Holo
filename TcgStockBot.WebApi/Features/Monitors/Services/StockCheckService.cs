using System.Diagnostics;
using TcgStockBot.Library.Enums;
using TcgStockBot.Library.Text;
using TcgStockBot.WebApi.Extensions.Globalization;
using TcgStockBot.WebApi.Features.Alerts.Models;
using TcgStockBot.WebApi.Features.Alerts.Services;
using TcgStockBot.WebApi.Features.Monitors.Models.DTOs;
using TcgStockBot.WebApi.Features.Notifications.Services;
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

        activity.CycleStarted();

        foreach (var store in stores.Where(static store => store.Enabled))
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
                var report = await SearchAsync(store, task, cancellationToken);
                activity.SearchCompleted(report);

                if (!report.Succeeded)
                {
                    failedSearches++;
                    continue;
                }

                completedSearches.Add(StockAlert.SearchKey(store.Name, task.Id));
                matches.AddRange(report.Listings
                    .Where(static listing => listing.Outcome.IsMatch())
                    .Select(listing => new StockAlert(task.Id, task.Name,
                        new StoreListing(store.Name, listing.Title, new Uri(listing.Url), listing.Price, listing.InStock, listing.ProductId, listing.Currency, listing.Seller),
                        LanguageToConfirm: listing.Outcome == MatchOutcome.MatchLanguageToConfirm)));
            }
        }

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
                activity.Log("alert", $"EM STOCK: {alert.Listing.Title} — {alert.Listing.StoreName} ({alert.Listing.Price:0.00} €)"
                    + (alert.LanguageToConfirm ? " · confirmar idioma" : string.Empty));
            }
        }

        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("Cycle done: {Searches} searches ok, {Failed} failed, {Matches} matching listings, {New} new alert(s)",
                completedSearches.Count, failedSearches, matches.Count, newAlerts.Count);

        activity.CycleFinished(completedSearches.Count + failedSearches, failedSearches, matches.Count, newAlerts.Count);
        return newAlerts;
    }

    private static List<StockTask> Rotate(List<StockTask> tasks, int offset) =>
        tasks.Count <= 1 ? tasks : [.. tasks.Skip(offset % tasks.Count), .. tasks.Take(offset % tasks.Count)];

    private static bool RunsOn(StockTask task, Store store) =>
        task.Enabled && (task.StoreNames.Count == 0 || task.StoreNames.Contains(store.Name, StringComparer.OrdinalIgnoreCase));

    private async Task<SearchReportDTO> SearchAsync(Store store, StockTask task, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var report = new SearchReportDTO { StoreName = store.Name, StockTaskId = task.Id };

        try
        {
            var listings = await storeAdapterResolver.Resolve(store).SearchAsync(store, task.SearchQuery, cancellationToken);

            report.Succeeded = true;
            report.Listings = listings.Select(listing => new ListingReportDTO
            {
                Title = listing.Title,
                Url = listing.Url.AbsoluteUri,
                Price = listing.Price,
                InStock = listing.InStock,
                Language = LanguageClassifier.Classify(listing.Title),
                Outcome = StockTaskMatcher.Evaluate(task, listing, store.UnmarkedLanguage),
                ProductId = listing.ProductId,
                Currency = listing.Currency,
                Seller = listing.Seller,
            }).ToList();
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            if (logger.IsEnabled(LogLevel.Warning))
                logger.LogWarning("Search failed on {Store} for '{Query}': {Error}", store.Name, task.SearchQuery, exception.Message);

            report.Succeeded = false;
            report.Error = await StoreFailure.DescribeAsync(exception, globalizationHelper);
        }

        report.CheckedAt = timeProvider.GetUtcNow();
        report.DurationMs = stopwatch.ElapsedMilliseconds;
        return report;
    }
}
