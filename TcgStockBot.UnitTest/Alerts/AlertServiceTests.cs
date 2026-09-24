using TcgStockBot.WebApi.Features.Alerts.Models;
using TcgStockBot.WebApi.Features.Alerts.Repositories;
using TcgStockBot.WebApi.Features.Alerts.Services;
using TcgStockBot.WebApi.Features.Stores.Services.Adapters;

namespace TcgStockBot.UnitTest.Alerts;

public sealed class AlertServiceTests : IDisposable
{
    private readonly TestDataContext _data = new();
    private readonly AlertService _service;

    public AlertServiceTests() =>
        _service = new AlertService(new AlertRepository(_data.Context), TimeProvider.System);

    public void Dispose() => _data.Dispose();

    private static StockAlert Alert(string store = "Shop", string task = "etb", bool languageToConfirm = false) =>
        new(task, task, new StoreListing(store, "Elite Trainer Box (EN)", new Uri($"https://{store.ToLowerInvariant()}.example/products/etb"), 54.99m, true), languageToConfirm);

    private static HashSet<string> Searched(params (string Store, string Task)[] searches) =>
        searches.Select(static search => StockAlert.SearchKey(search.Store, search.Task)).ToHashSet();

    [Fact]
    public async Task SameMatchTwice_AlertsOnlyOnce()
    {
        var searched = Searched(("Shop", "etb"));

        Assert.Single(await _service.SelectNewAlertsAsync([Alert()], searched));
        Assert.Empty(await _service.SelectNewAlertsAsync([Alert()], searched));
    }

    [Fact]
    public async Task SoldOutThenRestocked_AlertsAgain()
    {
        var searched = Searched(("Shop", "etb"));

        await _service.SelectNewAlertsAsync([Alert()], searched);
        await _service.SelectNewAlertsAsync([], searched);

        Assert.Single(await _service.SelectNewAlertsAsync([Alert()], searched));
    }

    [Fact]
    public async Task FailedSearch_KeepsItsMemory()
    {
        await _service.SelectNewAlertsAsync([Alert()], Searched(("Shop", "etb")));
        await _service.SelectNewAlertsAsync([], Searched(("Other", "etb")));

        Assert.Empty(await _service.SelectNewAlertsAsync([Alert()], Searched(("Shop", "etb"))));
    }

    [Fact]
    public async Task Reset_AlertsEverythingAgain()
    {
        var searched = Searched(("Shop", "etb"));
        await _service.SelectNewAlertsAsync([Alert()], searched);

        await _service.ResetAsync();

        Assert.Single(await _service.SelectNewAlertsAsync([Alert()], searched));
    }

    [Fact]
    public async Task Record_KeepsTheLanguageFlagInTheHistory()
    {
        await _service.RecordAsync([Alert(languageToConfirm: true)]);

        var alert = Assert.Single((await _service.ListAsync()).Result!);
        Assert.True(alert.LanguageToConfirm);
        Assert.Equal("etb", alert.StockTaskId);
    }
}
