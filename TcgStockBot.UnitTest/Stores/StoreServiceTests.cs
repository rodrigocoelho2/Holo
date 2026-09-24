using System.Net;
using TcgStockBot.Library.Enums;
using TcgStockBot.WebApi.Extensions.Globalization;
using TcgStockBot.WebApi.Features.StockTasks.Models.Entities;
using TcgStockBot.WebApi.Features.StockTasks.Repositories;
using TcgStockBot.WebApi.Features.Stores.Models.DTOs;
using TcgStockBot.WebApi.Features.Stores.Repositories;
using TcgStockBot.WebApi.Features.Stores.Services;
using TcgStockBot.WebApi.Features.Stores.Services.Adapters;

namespace TcgStockBot.UnitTest.Stores;

public sealed class StoreServiceTests : IDisposable
{
    private readonly TestDataContext _data = new();
    private readonly StockTaskRepository _taskRepository;
    private readonly StoreService _service;

    public StoreServiceTests()
    {
        _taskRepository = new StockTaskRepository(_data.Context);
        var factory = new FakeHttpClientFactory(new FakeHttpMessageHandler("blocked", "text/html", HttpStatusCode.Forbidden));
        var resolver = new StoreAdapterResolver([new ShopifyStoreAdapter(factory)]);
        _service = new StoreService(new StoreRepository(_data.Context), _taskRepository, resolver, new GlobalizationHelper());
    }

    public void Dispose() => _data.Dispose();

    private static StoreSaveRequest Shopify(string name, string? originalName = null) => new()
    {
        OriginalName = originalName,
        Name = name,
        Platform = StorePlatform.Shopify,
        BaseUrl = "https://shop.example",
        UnmarkedLanguage = UnmarkedLanguagePolicy.Confirm,
    };

    [Fact]
    public async Task Save_DuplicateName_IsRejected()
    {
        await _service.SaveAsync(Shopify("Shop"));

        var response = await _service.SaveAsync(Shopify("shop"));

        Assert.False(response.IsSuccess);
        Assert.Contains(response.Problems!.Errors, static error => error.Field == "Name");
    }

    [Fact]
    public async Task Save_Rename_UpdatesTasksThatUseTheStore()
    {
        await _service.SaveAsync(Shopify("Old"));
        await _taskRepository.SaveAsync(new StockTask { Id = "etb", Name = "ETB", SearchQuery = "etb", MaxPrice = 70, StoreNames = ["Old"] });

        var response = await _service.SaveAsync(Shopify("New", originalName: "Old"));

        Assert.True(response.IsSuccess);
        Assert.Equal(["New"], (await _taskRepository.GetAsync("etb"))!.StoreNames);
        Assert.Equal(UnmarkedLanguagePolicy.Confirm, Assert.Single((await _service.ListAsync()).Result!).UnmarkedLanguage);
    }

    [Fact]
    public async Task Delete_RemovesTheStoreFromTasks()
    {
        await _service.SaveAsync(Shopify("Shop"));
        await _taskRepository.SaveAsync(new StockTask { Id = "etb", Name = "ETB", SearchQuery = "etb", MaxPrice = 70, StoreNames = ["Shop"] });

        await _service.DeleteAsync("Shop");

        Assert.Empty((await _service.ListAsync()).Result!);
        Assert.Empty((await _taskRepository.GetAsync("etb"))!.StoreNames);
    }

    [Fact]
    public async Task Save_HtmlWithoutSelectors_IsRejected()
    {
        var request = Shopify("Html");
        request.Platform = StorePlatform.Html;

        var response = await _service.SaveAsync(request);

        Assert.Equal(["SearchUrlTemplate", "ProductSelector"], response.Problems!.Errors.Select(static error => error.Field));
    }

    [Fact]
    public async Task Probe_StoreBlocksUs_ReturnsATranslatedReason()
    {
        var response = await _service.ProbeAsync(new StoreProbeRequest { Store = Shopify("Shop"), Query = "etb" });

        Assert.False(response.IsSuccess);
        Assert.Contains("403", response.Problems!.Title);
    }
}
