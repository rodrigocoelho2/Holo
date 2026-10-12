using TcgStockBot.WebApi.Features.PriceHistories.Models.Entities;
using TcgStockBot.WebApi.Features.PriceHistories.Services;

namespace TcgStockBot.UnitTest.PriceHistories;

/// <summary>
/// Restock patterns: sold out → in stock changes, grouped into deliveries and counted by weekday and hour.
/// </summary>
public sealed class RestockPatternTests
{
    // 2026-09-01 is a Tuesday.
    private static readonly DateTimeOffset Start = new(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);

    private static PriceHistoryEntry Product(string store, params (DateTimeOffset At, bool InStock)[] points) => new()
    {
        StoreName = store,
        FirstSeen = points[0].At,
        LastSeen = points[^1].At,
        Points = [.. points.Select(static point => new PricePoint { At = point.At, Price = 59.99m, InStock = point.InStock })],
    };

    /// <summary>One product that sells out and comes back on the given days, at 10:15 UTC.</summary>
    private static PriceHistoryEntry Restocked(string store, params int[] days) =>
        Product(store, [.. days.SelectMany(day => new[] { (Start.AddDays(day - 1).AddHours(20), false), (Start.AddDays(day).AddHours(10.25), true) })]);

    [Fact]
    public void OnlySoldOutToInStock_Counts()
    {
        var entry = Product("Shop", (Start, true), (Start.AddHours(5), false), (Start.AddDays(1).AddHours(9), true), (Start.AddDays(2), true));

        var pattern = Assert.Single(RestockPatterns.Find([entry], TimeZoneInfo.Utc, Start.AddDays(10)));

        Assert.Equal(1, pattern.Restocks);
        Assert.Equal(Start.AddDays(1).AddHours(9), pattern.LastRestockAt);
        Assert.Equal(1, pattern.ByWeekday[2]);   // Wednesday
        Assert.Equal(1, pattern.ByHour[9]);
        Assert.False(pattern.Confident);
        Assert.Empty(pattern.UsualWeekdays);
        Assert.Null(pattern.UsualHour);
    }

    [Fact]
    public void ProductsBackTogether_AreOneRestock()
    {
        var first = Product("Shop", (Start, false), (Start.AddHours(10), true));
        var second = Product("Shop", (Start, false), (Start.AddHours(11.5), true));
        var later = Product("Shop", (Start, false), (Start.AddHours(16), true));

        var pattern = Assert.Single(RestockPatterns.Find([first, second, later], TimeZoneInfo.Utc, Start.AddDays(1)));

        Assert.Equal(2, pattern.Restocks);
        Assert.Equal(3, pattern.Products);
    }

    [Fact]
    public void ARegularStore_HasAUsualDayAndHour()
    {
        // Tuesdays (days 0, 7, 14, 21) and one Thursday (day 9), always 10:15.
        var pattern = Assert.Single(RestockPatterns.Find([Restocked("Shop", 0, 7, 14, 21, 9)], TimeZoneInfo.Utc, Start.AddDays(30)));

        Assert.True(pattern.Confident);
        Assert.Equal(5, pattern.Restocks);
        Assert.Equal([1], pattern.UsualWeekdays);   // Tuesday; Thursday has 1 of 5, under a quarter
        Assert.Equal(10, pattern.UsualHour);        // 10–12: starts at the hour the restocks happen
        Assert.Equal(4, pattern.ByWeekdayHour[1][10]);
    }

    [Fact]
    public void Stores_AreSeparate_MostRestocksFirst()
    {
        var patterns = RestockPatterns.Find([Restocked("Small", 3), Restocked("Big", 1, 2, 3)], TimeZoneInfo.Utc, Start.AddDays(5));

        Assert.Equal(["Big", "Small"], patterns.Select(static pattern => pattern.StoreName));
    }

    [Fact]
    public void Hours_AreInTheLocalTimeZone()
    {
        var lisbon = TimeZoneInfo.FindSystemTimeZoneById("Europe/Lisbon");   // UTC+1 in September

        var pattern = Assert.Single(RestockPatterns.Find([Product("Shop", (Start, false), (Start.AddHours(23.5), true))], lisbon, Start.AddDays(2)));

        Assert.Equal(1, pattern.ByHour[0]);
        Assert.Equal(1, pattern.ByWeekday[2]);   // Wednesday 00:30 in Lisbon
    }
}
