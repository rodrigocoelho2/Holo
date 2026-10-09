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

    [Theory]
    [InlineData("", "AwinFeedKey")]
    [InlineData("Media Markt", "AwinFeedKey")]
    [InlineData("MediaMarktES", null)]
    public async Task Save_Awin_NeedsASafeFeedKey(string feedKey, string? expectedError)
    {
        var request = Shopify("MediaMarkt");
        request.Platform = StorePlatform.Awin;
        request.AwinFeedKey = feedKey;

        var response = await _service.SaveAsync(request);

        if (expectedError is null)
        {
            Assert.True(response.IsSuccess);
            Assert.Equal("MediaMarktES", Assert.Single((await _service.ListAsync()).Result!).AwinFeedKey);
        }
        else
        {
            Assert.Contains(response.Problems!.Errors, error => error.Field == expectedError);
        }
    }

    [Fact]
    public async Task Save_KeepsTheClassification_AndCleansTheTags()
    {
        var request = Shopify("Loja ES");
        request.Country = StoreCountry.Spain;
        request.Tier = StoreTier.A;
        request.Type = StoreType.Specialist;
        request.Tags = [" por testar ", "Por Testar", "", "envia para PT"];
        request.Notes = "  nota  ";

        await _service.SaveAsync(request);

        var store = Assert.Single((await _service.ListAsync()).Result!);
        Assert.Equal((StoreCountry.Spain, StoreTier.A, StoreType.Specialist), (store.Country, store.Tier, store.Type));
        Assert.Equal(["por testar", "envia para PT"], store.Tags);
        Assert.Equal("nota", store.Notes);
    }

    [Fact]
    public async Task Probe_StoreWithoutIntegration_SaysSo()
    {
        var resolver = new StoreAdapterResolver([new ManualStoreAdapter()]);
        var service = new StoreService(new StoreRepository(_data.Context), _taskRepository, resolver, new GlobalizationHelper());
        var request = Shopify("Cardmarket");
        request.Platform = StorePlatform.Manual;

        var response = await service.ProbeAsync(new StoreProbeRequest { Store = request, Query = "etb" });

        Assert.False(response.IsSuccess);
        Assert.Contains("não tem integração", response.Problems!.Title);
    }
}
