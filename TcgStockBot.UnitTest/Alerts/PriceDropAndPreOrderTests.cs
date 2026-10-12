using System.Text.Json;
using TcgStockBot.Library.Text;
using TcgStockBot.WebApi.Extensions.Data;
using TcgStockBot.WebApi.Features.Alerts.Models;
using TcgStockBot.WebApi.Features.Alerts.Models.Entities;
using TcgStockBot.WebApi.Features.Alerts.Repositories;
using TcgStockBot.WebApi.Features.Alerts.Services;
using TcgStockBot.WebApi.Features.Notifications.Services;
using TcgStockBot.WebApi.Features.Stores.Services.Adapters;
using TcgStockBot.WebApi.Features.Stores.Services.Adapters.Awin;

namespace TcgStockBot.UnitTest.Alerts;

/// <summary>
/// Price-drop alerts (memory of the last alerted price, v3.0 alert-state.json still readable) and pre-order marks.
/// </summary>
public sealed class PriceDropAndPreOrderTests : IDisposable
{
    private readonly TestDataContext _data = new();
    private readonly AlertService _service;

    public PriceDropAndPreOrderTests() =>
        _service = new AlertService(new AlertRepository(_data.Context), TimeProvider.System);

    public void Dispose() => _data.Dispose();

    private static StockAlert Alert(decimal price, bool preOrder = false) =>
        new("rb-display", "Riftbound Booster Display",
            new StoreListing("Shop", "Riftbound Radiance Booster Box", new Uri("https://shop.example/products/radiance"), price, true, PreOrder: preOrder), false);

    private static readonly HashSet<string> Searched = [StockAlert.SearchKey("Shop", "rb-display")];

    [Fact]
    public async Task LowerPrice_AlertsAgainOnce_WithThePreviousPrice()
    {
        Assert.Single(await _service.SelectNewAlertsAsync([Alert(130m)], Searched));

        var drop = Assert.Single(await _service.SelectNewAlertsAsync([Alert(115m)], Searched));
        Assert.True(drop.IsPriceDrop);
        Assert.Equal(130m, drop.PreviousPrice);

        Assert.Empty(await _service.SelectNewAlertsAsync([Alert(115m)], Searched));   // same price: quiet
    }

    [Theory]
    [InlineData(130, 129.99)]   // below the 0,50 € minimum
    [InlineData(5.49, 5.29)]    // below the 0,50 € minimum
    [InlineData(130, 135)]      // a rise never alerts
    public async Task SmallDropsAndRises_StayQuiet(double first, double second)
    {
        await _service.SelectNewAlertsAsync([Alert((decimal)first)], Searched);

        Assert.Empty(await _service.SelectNewAlertsAsync([Alert((decimal)second)], Searched));
    }

    [Fact]
    public async Task PriceGoingUpAndBackDown_DoesNotAlertTwice()
    {
        await _service.SelectNewAlertsAsync([Alert(130m)], Searched);
        await _service.SelectNewAlertsAsync([Alert(140m)], Searched);

        Assert.Empty(await _service.SelectNewAlertsAsync([Alert(130m)], Searched));   // back to the alerted price
        Assert.Single(await _service.SelectNewAlertsAsync([Alert(120m)], Searched));  // below it: alerts
    }

    [Fact]
    public async Task DropAlert_IsRecordedWithPreviousPriceAndPreOrder()
    {
        await _service.SelectNewAlertsAsync([Alert(130m)], Searched);
        var drops = await _service.SelectNewAlertsAsync([Alert(115m, preOrder: true)], Searched);

        await _service.RecordAsync(drops);

        var recorded = Assert.Single((await _service.ListAsync()).Result!);
        Assert.Equal(130m, recorded.PreviousPrice);
        Assert.True(recorded.PreOrder);
    }

    [Fact]
    public async Task V30AlertState_IsStillRead_AndStartsTrackingPrices()
    {
        var key = Alert(130m).Key;
        await File.WriteAllTextAsync(_data.Context.AlertStatePath, JsonSerializer.Serialize(new Dictionary<string, string> { [key] = "2026-09-20T10:00:00+00:00" }));

        Assert.Empty(await _service.SelectNewAlertsAsync([Alert(130m)], Searched));   // already alerted in v3.0
        Assert.Single(await _service.SelectNewAlertsAsync([Alert(115m)], Searched));  // price now remembered → drop alerts

        var saved = await JsonFile.ReadAsync<Dictionary<string, AlertMemory>>(_data.Context.AlertStatePath, CancellationToken.None);
        Assert.Equal(new DateTimeOffset(2026, 9, 20, 10, 0, 0, TimeSpan.Zero), saved![key].AlertedAt);
        Assert.Equal(115m, saved[key].Price);
    }

    [Fact]
    public void Messages_ShowTheDropAndThePreOrder()
    {
        var alert = Alert(115m, preOrder: true) with { PreviousPrice = 130m };

        var whatsApp = AlertMessageFormatter.WhatsApp([alert], 5);
        Assert.Contains("price drop", whatsApp);
        Assert.Contains("€115.00 (was €130.00)", whatsApp);
        Assert.Contains("Pre-order", whatsApp);
        Assert.StartsWith("[Holo] Price drop:", AlertMessageFormatter.Subject([alert]));
        Assert.StartsWith("[Holo] Pre-order:", AlertMessageFormatter.Subject([Alert(115m, preOrder: true)]));
    }

    [Theory]
    [InlineData("Pokémon TCG: Ascended Heroes ETB [Pré-venda]", true)]
    [InlineData("PRE-ORDER - Perfect Order Booster Box", true)]
    [InlineData("Riftbound Radiance Booster Box (Preorder)", true)]
    [InlineData("One Piece OP-16 Booster Box - Reserva", true)]
    [InlineData("Caja de sobres - Preventa", true)]
    [InlineData("Perfect Order Booster Box", false)]
    [InlineData("Prevent Damage Deck", false)]   // "prevent" is not "preventa"
    public void PreOrder_IsRecognisedFromTheTitle(string title, bool expected) =>
        Assert.Equal(expected, PreOrderDetector.IsPreOrder(title));

    [Theory]
    [InlineData("preorder", true)]
    [InlineData("Pre-Order", true)]
    [InlineData("pre_order", true)]
    [InlineData("in stock", false)]
    [InlineData(null, false)]
    public void AwinStockStatus_PreOrder(string? status, bool expected) =>
        Assert.Equal(expected, AwinFeedCatalog.IsPreOrderStatus(status));
}
