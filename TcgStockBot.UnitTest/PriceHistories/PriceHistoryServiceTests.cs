using TcgStockBot.Library.Services;
using TcgStockBot.WebApi.Extensions.Globalization;
using TcgStockBot.WebApi.Features.PriceHistories.Models.Entities;
using TcgStockBot.WebApi.Features.PriceHistories.Repositories;
using TcgStockBot.WebApi.Features.PriceHistories.Services;

namespace TcgStockBot.UnitTest.PriceHistories;

/// <summary>
/// price-history.json: a point only on change, size limits, retention, lowest price and reload from disk.
/// </summary>
public sealed class PriceHistoryServiceTests : IDisposable
{
    private readonly TestDataContext _data = new();
    private readonly ManualClock _clock = new(new DateTimeOffset(2026, 9, 1, 10, 0, 0, TimeSpan.Zero));

    public void Dispose() => _data.Dispose();

    private PriceHistoryService CreateService() =>
        new(new PriceHistoryRepository(_data.Context), new GlobalizationHelper(), _clock);

    private static PriceObservation Seen(decimal price, bool inStock = true, string url = "https://shop.example/products/radiance?variant=1") =>
        new("Shop", "Riftbound Radiance Booster Box", url, "SKU-1", "EUR", price, inStock);

    [Fact]
    public async Task OnlyChangesAddPoints()
    {
        var service = CreateService();

        await service.RecordAsync([Seen(130m)]);
        _clock.Advance(TimeSpan.FromMinutes(10));
        await service.RecordAsync([Seen(130m)]);                  // same: no point, LastSeen moves
        _clock.Advance(TimeSpan.FromMinutes(10));
        await service.RecordAsync([Seen(130m, inStock: false)]);  // stock changed
        _clock.Advance(TimeSpan.FromMinutes(10));
        await service.RecordAsync([Seen(115m, inStock: false)]);  // price changed

        var history = (await service.GetAsync(Seen(0).Key)).Result!;
        Assert.Equal([130m, 130m, 115m], history.Points.Select(static point => point.Price));
        Assert.Equal([true, false, false], history.Points.Select(static point => point.InStock));
        Assert.Equal(115m, history.Summary.CurrentPrice);
        Assert.Equal(115m, history.Summary.LowestPrice);
        Assert.Equal(130m, history.Summary.HighestPrice);
        Assert.Equal(_clock.GetUtcNow(), history.Summary.LastSeen);
    }

    [Fact]
    public async Task SameProductFromTwoTasksOrUrlsWithQuery_IsOneHistory()
    {
        var service = CreateService();

        await service.RecordAsync([Seen(130m), Seen(130m, url: "https://SHOP.example/products/radiance?utm=x")]);

        var summary = Assert.Single((await service.ListAsync()).Result!);
        Assert.Equal("shop|https://shop.example/products/radiance", summary.Key);
    }

    [Fact]
    public async Task History_SurvivesARestart()
    {
        await CreateService().RecordAsync([Seen(130m)]);

        var reloaded = CreateService();   // new instance → reads price-history.json

        Assert.Equal(130m, (await reloaded.LowestPricesAsync())[Seen(0).Key]);
    }

    [Fact]
    public async Task PointsAreCapped_OldestFirst()
    {
        var service = CreateService();

        for (var index = 0; index < PriceHistoryService.MaxPointsPerProduct + 20; index++)
        {
            await service.RecordAsync([Seen(100m + index)]);
        }

        var history = (await service.GetAsync(Seen(0).Key)).Result!;
        Assert.Equal(PriceHistoryService.MaxPointsPerProduct, history.Points.Count);
        Assert.Equal(120m, history.Points[0].Price);
    }

    [Fact]
    public async Task ProductsNotSeenForTheRetention_AreDropped()
    {
        var service = CreateService();
        await service.RecordAsync([Seen(130m)]);

        _clock.Advance(PriceHistoryService.Retention + TimeSpan.FromDays(1));
        await service.RecordAsync([Seen(50m, url: "https://shop.example/products/other")]);

        var summary = Assert.Single((await service.ListAsync()).Result!);
        Assert.EndsWith("/products/other", summary.Url);
    }

    [Fact]
    public async Task UnknownOrMissingIdentifier_IsReported()
    {
        var service = CreateService();

        Assert.Equal(ServiceApiResponseStatus.NotFound, (await service.GetAsync("shop|https://nope.example/x")).Status);
        Assert.Equal(ServiceApiResponseStatus.ValidationProblems, (await service.GetAsync(" ")).Status);
    }

    /// <summary>A clock the test moves by hand.</summary>
    private sealed class ManualClock(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }
}
