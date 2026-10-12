using TcgStockBot.WebApi.Features.PriceHistories.Models.DTOs;
using TcgStockBot.WebApi.Features.PriceHistories.Models.Entities;

namespace TcgStockBot.WebApi.Features.PriceHistories.Services;

/// <summary>
/// Finds when each store restocks: every sold out → in stock change in the history, grouped into restocks (products
/// back within <see cref="SameRestock"/> of the first one are one delivery), then counted by weekday and hour.
/// </summary>
internal static class RestockPatterns
{
    /// <summary>Products back in stock within this time of each other are one restock.</summary>
    internal static readonly TimeSpan SameRestock = TimeSpan.FromHours(3);

    /// <summary>Restocks needed before a pattern is shown as one.</summary>
    internal const int ConfidentFrom = 5;

    /// <summary>
    /// The restock pattern of every store with at least one restock, most restocks first.
    /// </summary>
    /// <param name="entries">The price history.</param>
    /// <param name="timeZone">The local time zone for weekdays and hours.</param>
    /// <param name="now">Now, for the number of days covered.</param>
    /// <returns>One pattern per store.</returns>
    public static List<StoreRestockDTO> Find(IEnumerable<PriceHistoryEntry> entries, TimeZoneInfo timeZone, DateTimeOffset now)
    {
        var byStore = entries
            .Where(static entry => entry.Points.Count > 0)
            .GroupBy(static entry => entry.StoreName, StringComparer.OrdinalIgnoreCase);

        var patterns = new List<StoreRestockDTO>();
        foreach (var store in byStore)
        {
            var restocks = store.SelectMany(static entry => entry.Points.Zip(entry.Points.Skip(1))
                    .Where(static pair => !pair.First.InStock && pair.Second.InStock)
                    .Select(static pair => pair.Second.At))
                .Order()
                .ToList();
            if (restocks.Count == 0)
            {
                continue;
            }

            var waves = new List<DateTimeOffset>();
            foreach (var at in restocks)
            {
                if (waves.Count == 0 || at - waves[^1] > SameRestock)
                {
                    waves.Add(at);
                }
            }

            var byWeekdayHour = Enumerable.Range(0, 7).Select(static _ => new int[24]).ToArray();
            foreach (var wave in waves)
            {
                var local = TimeZoneInfo.ConvertTime(wave, timeZone);
                byWeekdayHour[((int)local.DayOfWeek + 6) % 7][local.Hour]++;
            }

            var byWeekday = byWeekdayHour.Select(static day => day.Sum()).ToList();
            var byHour = Enumerable.Range(0, 24).Select(hour => byWeekdayHour.Sum(day => day[hour])).ToList();
            var confident = waves.Count >= ConfidentFrom;
            var firstSeen = store.Min(static entry => entry.FirstSeen);

            patterns.Add(new StoreRestockDTO
            {
                StoreName = store.First().StoreName,
                Restocks = waves.Count,
                Products = restocks.Count,
                Days = Math.Max(1, (int)Math.Ceiling((now - firstSeen).TotalDays)),
                LastRestockAt = waves[^1],
                ByWeekday = byWeekday,
                ByHour = byHour,
                ByWeekdayHour = [.. byWeekdayHour.Select(static day => day.ToList())],
                UsualWeekdays = confident ? UsualWeekdays(byWeekday, waves.Count) : [],
                UsualHour = confident ? UsualHour(byHour, waves.Count) : null,
                Confident = confident,
                TimeZone = timeZone.Id,
            });
        }

        return [.. patterns.OrderByDescending(static pattern => pattern.Restocks).ThenBy(static pattern => pattern.StoreName, StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>The one or two weekdays that hold at least a quarter of the restocks each (twice the even share).</summary>
    private static List<int> UsualWeekdays(List<int> byWeekday, int total) =>
        [.. byWeekday.Select(static (count, day) => (Count: count, Day: day))
            .Where(item => item.Count >= 2 && item.Count * 4 >= total)
            .OrderByDescending(static item => item.Count).ThenBy(static item => item.Day)
            .Take(2)
            .Select(static item => item.Day)];

    /// <summary>The start of the busiest two-hour window, when it holds at least a third of the restocks.</summary>
    private static int? UsualHour(List<int> byHour, int total)
    {
        var best = Enumerable.Range(0, 24)
            .Select(hour => (Hour: hour, Count: byHour[hour] + byHour[(hour + 1) % 24]))
            .OrderByDescending(static window => window.Count).ThenByDescending(window => byHour[window.Hour]).ThenBy(static window => window.Hour)
            .First();
        return best.Count * 3 >= total && best.Count >= 2 ? best.Hour : null;
    }
}
