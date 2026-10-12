using TcgStockBot.Library.Enums;
using TcgStockBot.WebApi.Extensions.Globalization;
using TcgStockBot.WebApi.Features.Alerts.Models;
using TcgStockBot.WebApi.Features.Notifications.Services;
using TcgStockBot.WebApi.Features.StockTasks.Repositories;
using TcgStockBot.WebApi.Features.Stores.Models.DTOs;
using TcgStockBot.WebApi.Features.Stores.Models.Entities;
using TcgStockBot.WebApi.Features.Stores.Repositories;
using TcgStockBot.WebApi.Features.Stores.Services;
using TcgStockBot.WebApi.Features.Stores.Services.Adapters;

namespace TcgStockBot.UnitTest.Stores;

/// <summary>
/// Shipping per store: what an order of one product costs to ship, kept on the store and shown in the alerts.
/// </summary>
public sealed class ShippingTests : IDisposable
{
    private readonly TestDataContext _data = new();

    public void Dispose() => _data.Dispose();

    [Theory]
    [InlineData(null, null, 50, null)]
    [InlineData(4.99, null, 50, 4.99)]
    [InlineData(4.99, 60, 59.99, 4.99)]
    [InlineData(4.99, 60, 60, 0)]
    [InlineData(4.99, 60, 179.99, 0)]
    [InlineData(0, null, 10, 0)]
    public void ShippingFor_IsFreeFromTheThreshold(double? cost, double? freeFrom, double price, double? expected)
    {
        var store = new Store { ShippingCost = (decimal?)cost, FreeShippingFrom = (decimal?)freeFrom };

        Assert.Equal((decimal?)expected, store.ShippingFor((decimal)price));
    }

    [Fact]
    public async Task Save_KeepsTheShipping_AndDropsAZeroThreshold()
    {
        var service = Service();

        var response = await service.SaveAsync(new StoreSaveRequest
        {
            Name = "Shop", Platform = StorePlatform.Shopify, BaseUrl = "https://shop.example", ShippingCost = 4.994m, FreeShippingFrom = 0m,
        });

        Assert.True(response.IsSuccess);
        var saved = Assert.Single((await service.ListAsync()).Result!);
        Assert.Equal(4.99m, saved.ShippingCost);
        Assert.Null(saved.FreeShippingFrom);
    }

    [Theory]
    [InlineData(-1, null, "ShippingCost")]
    [InlineData(4.99, 900, "FreeShippingFrom")]
    public async Task Save_ShippingOutOfRange_IsRejected(double cost, double? freeFrom, string field)
    {
        var response = await Service().SaveAsync(new StoreSaveRequest
        {
            Name = "Shop", Platform = StorePlatform.Shopify, BaseUrl = "https://shop.example", ShippingCost = (decimal)cost, FreeShippingFrom = (decimal?)freeFrom,
        });

        Assert.False(response.IsSuccess);
        Assert.Contains(response.Problems!.Errors, error => error.Field == field);
    }

    [Theory]
    [InlineData(4.99, "€54.99 + €4.99 shipping")]
    [InlineData(0, "€54.99 · free shipping")]
    public void Alerts_SayTheShipping(double shipping, string expected)
    {
        var alert = new StockAlert("etb", "ETB", new StoreListing("Shop", "Pitch Black Elite Trainer Box", new Uri("https://shop.example/p"), 54.99m, true),
            LanguageToConfirm: false, Shipping: (decimal)shipping);

        Assert.Contains(expected, AlertMessageFormatter.WhatsApp([alert], 5));
    }

    [Fact]
    public void Alerts_WithoutKnownShipping_SayNothing() =>
        Assert.DoesNotContain("shipping", AlertMessageFormatter.WhatsApp(
            [new StockAlert("etb", "ETB", new StoreListing("Shop", "ETB", new Uri("https://shop.example/p"), 54.99m, true), LanguageToConfirm: false)], 5));

    private StoreService Service() =>
        new(new StoreRepository(_data.Context), new StockTaskRepository(_data.Context), new StoreAdapterResolver([]), new GlobalizationHelper());
}
