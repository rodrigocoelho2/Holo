using TcgStockBot.Library.Enums;
using TcgStockBot.Library.Services;
using TcgStockBot.WebApi.Extensions.Globalization;
using TcgStockBot.WebApi.Features.StockTasks.Models.DTOs;
using TcgStockBot.WebApi.Features.StockTasks.Repositories;
using TcgStockBot.WebApi.Features.StockTasks.Services;

namespace TcgStockBot.UnitTest.StockTasks;

public sealed class StockTaskServiceTests : IDisposable
{
    private readonly TestDataContext _data = new();
    private readonly StockTaskService _service;

    public StockTaskServiceTests() =>
        _service = new StockTaskService(new StockTaskRepository(_data.Context), new GlobalizationHelper());

    public void Dispose() => _data.Dispose();

    private static StockTaskSaveRequest ValidRequest(string id = "etb", bool isNew = true) => new()
    {
        IsNew = isNew,
        Id = id,
        Name = "Elite Trainer Box",
        SearchQuery = "elite trainer box",
        RequiredKeywords = ["elite trainer box", " ", "elite trainer box"],
        MaxPrice = 70m,
    };

    [Fact]
    public async Task Save_New_StoresAndCleansTheTask()
    {
        var response = await _service.SaveAsync(ValidRequest());

        Assert.True(response.IsSuccess);
        var saved = Assert.Single((await _service.ListAsync()).Result!);
        Assert.Equal(["elite trainer box"], saved.RequiredKeywords);
    }

    [Fact]
    public async Task Save_KeepsTheGame_AndDefaultsToPokemon()
    {
        var onePiece = ValidRequest("op");
        onePiece.Game = TcgGame.OnePiece;
        await _service.SaveAsync(onePiece);
        await _service.SaveAsync(ValidRequest("etb"));

        Assert.Equal(TcgGame.OnePiece, (await _service.GetAsync("op")).Result!.Game);
        Assert.Equal(TcgGame.Pokemon, (await _service.GetAsync("etb")).Result!.Game);
    }

    [Fact]
    public async Task Save_InvalidFields_ReturnsOneErrorPerField()
    {
        var response = await _service.SaveAsync(new StockTaskSaveRequest { IsNew = true, Id = "a|b", Name = "", SearchQuery = "", MaxPrice = 0 });

        Assert.Equal(ServiceApiResponseStatus.ValidationProblems, response.Status);
        Assert.Equal(["Identifier", "Name", "SearchQuery", "MaxPrice"], response.Problems!.Errors.Select(static error => error.Field));
        Assert.All(response.Problems.Errors, static error => Assert.False(string.IsNullOrWhiteSpace(error.Message)));
    }

    [Fact]
    public async Task Save_MsrpRule_AllowsNoFixedMaximum_AndKeepsKinds()
    {
        var request = ValidRequest("set");
        request.PriceRule = PriceRule.Msrp;
        request.MaxPrice = 0m;
        request.MsrpTolerancePercent = 15;
        request.Kinds = ["ETB", " booster-bundle ", "etb"];

        var response = await _service.SaveAsync(request);

        Assert.True(response.IsSuccess);
        var saved = (await _service.GetAsync("set")).Result!;
        Assert.Equal(PriceRule.Msrp, saved.PriceRule);
        Assert.Equal(15, saved.MsrpTolerancePercent);
        Assert.Equal(["etb", "booster-bundle"], saved.Kinds);
    }

    [Fact]
    public async Task Save_UnknownKindOrToleranceOutOfRange_IsRejected()
    {
        var request = ValidRequest("set");
        request.PriceRule = PriceRule.Msrp;
        request.MsrpTolerancePercent = 500;
        request.Kinds = ["etb", "not-a-kind"];

        var response = await _service.SaveAsync(request);

        Assert.Equal(["MsrpTolerancePercent", "Kinds"], response.Problems!.Errors.Select(static error => error.Field));
    }

    [Fact]
    public async Task Save_NewWithExistingId_IsRejected()
    {
        await _service.SaveAsync(ValidRequest());

        var response = await _service.SaveAsync(ValidRequest());

        Assert.False(response.IsSuccess);
        Assert.Contains(response.Problems!.Errors, static error => error.Field == "Identifier");
    }

    [Fact]
    public async Task Save_UpdateOfMissingTask_IsRejected()
    {
        var response = await _service.SaveAsync(ValidRequest(isNew: false));

        Assert.False(response.IsSuccess);
    }

    [Fact]
    public async Task Enable_AllTasks_StopsEveryTask()
    {
        await _service.SaveAsync(ValidRequest("a"));
        await _service.SaveAsync(ValidRequest("b"));

        await _service.EnableAsync(new StockTaskEnableRequest { Id = null, Enabled = false });

        Assert.All((await _service.ListAsync()).Result!, static task => Assert.False(task.Enabled));
    }

    [Fact]
    public async Task Get_Missing_IsNotFound() =>
        Assert.Equal(ServiceApiResponseStatus.NotFound, (await _service.GetAsync("nope")).Status);
}
