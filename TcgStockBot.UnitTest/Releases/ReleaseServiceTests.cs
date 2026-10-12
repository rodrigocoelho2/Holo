using TcgStockBot.Library.Enums;
using TcgStockBot.Library.Services;
using TcgStockBot.WebApi.Extensions.Globalization;
using TcgStockBot.WebApi.Features.Releases.Models.DTOs;
using TcgStockBot.WebApi.Features.Releases.Repositories;
using TcgStockBot.WebApi.Features.Releases.Services;
using TcgStockBot.WebApi.Features.Sets.Repositories;
using TcgStockBot.WebApi.Features.Sets.Services;
using TcgStockBot.WebApi.Features.StockTasks.Models.Entities;
using TcgStockBot.WebApi.Features.StockTasks.Repositories;

namespace TcgStockBot.UnitTest.Releases;

/// <summary>
/// The launch calendar: validation, order, edit and delete, persisted in releases.json.
/// </summary>
public sealed class ReleaseServiceTests : IDisposable
{
    private readonly TestDataContext _data = new();
    private readonly FakeTaskRepository _tasks = new();
    private readonly ReleaseService _service;

    public ReleaseServiceTests()
    {
        _service = new ReleaseService(new ReleaseRepository(_data.Context), _tasks, new GlobalizationHelper());
        _tasks.Items.Add(new StockTask { Id = "set-pitch-black", Name = "Pitch Black" });
    }

    public void Dispose() => _data.Dispose();

    private static ReleaseSaveRequest Request(string name, string date, string? preOrder = null) => new()
    {
        Name = name,
        Game = TcgGame.Pokemon,
        ReleaseDate = DateOnly.Parse(date, System.Globalization.CultureInfo.InvariantCulture),
        PreOrderDate = preOrder is null ? null : DateOnly.Parse(preOrder, System.Globalization.CultureInfo.InvariantCulture),
    };

    [Fact]
    public async Task Save_ListsSoonestFirst_AndPersists()
    {
        await _service.SaveAsync(Request("Depois", "2026-12-05"));
        await _service.SaveAsync(Request("Antes", "2026-11-14", "2026-10-20"));

        var again = new ReleaseService(new ReleaseRepository(_data.Context), _tasks, new GlobalizationHelper());
        var list = (await again.ListAsync()).Result!;

        Assert.Equal(["Antes", "Depois"], list.Select(static release => release.Name));
        Assert.Equal(new DateOnly(2026, 10, 20), list[0].PreOrderDate);
    }

    [Fact]
    public async Task Save_RejectsMissingNameAndDate_PreOrderAfterRelease_BadUrl_AndUnknownTask()
    {
        var empty = await _service.SaveAsync(new ReleaseSaveRequest());
        Assert.Equal(ServiceApiResponseStatus.ValidationProblems, empty.Status);
        Assert.Equal(["Name", "ReleaseDate"], empty.Problems!.Errors.Select(static error => error.Field));

        var request = Request("X", "2026-11-14", "2026-11-20");
        request.Url = "ftp://example.com";
        request.StockTaskId = "nope";
        var bad = await _service.SaveAsync(request);
        Assert.Equal(["PreOrderDate", "Url", "StockTaskId"], bad.Problems!.Errors.Select(static error => error.Field));
    }

    [Fact]
    public async Task Save_WithId_Replaces_AndDelete_Removes()
    {
        var created = (await _service.SaveAsync(Request("Pitch Black", "2026-11-14"))).Result!;

        var edit = Request("Pitch Black (EN)", "2026-11-21");
        edit.Id = created.Id;
        edit.StockTaskId = "set-pitch-black";
        edit.Url = "https://example.com/pitch-black";
        var saved = (await _service.SaveAsync(edit)).Result!;

        Assert.Equal(created.Id, saved.Id);
        Assert.Equal("set-pitch-black", saved.StockTaskId);
        Assert.Single((await _service.ListAsync()).Result!);

        Assert.Equal(ServiceApiResponseStatus.Success, (await _service.DeleteAsync(created.Id)).Status);
        Assert.Empty((await _service.ListAsync()).Result!);
        Assert.Equal(ServiceApiResponseStatus.NotFound, (await _service.DeleteAsync(created.Id)).Status);
    }

    [Fact]
    public async Task KnownSets_AreInTheCalendar_UnlessAReleaseCoversThem()
    {
        var repository = new ReleaseRepository(_data.Context);
        var withSets = new ReleaseService(repository, _tasks, new GlobalizationHelper(), new SetService(new SetRepository(), repository));
        await withSets.SaveAsync(Request("Pokémon — Pitch Black", "2026-07-17", "2026-06-01"));

        var list = (await withSets.ListAsync()).Result!;

        var pitchBlack = Assert.Single(list, static release => release.SetId == "pkm-me05");
        Assert.False(pitchBlack.BuiltIn);
        var chaosRising = Assert.Single(list, static release => release.SetId == "pkm-me04");
        Assert.True(chaosRising.BuiltIn);
        Assert.Equal("set:pkm-me04", chaosRising.Id);
        Assert.Equal(new DateOnly(2026, 5, 22), chaosRising.ReleaseDate);
        Assert.Contains(list, static release => release.SetId == "rb-rad");
        Assert.DoesNotContain(list, static release => release.SetId == "cp-wnc");   // no date
        Assert.Equal(list.OrderBy(static release => release.ReleaseDate).Select(static release => release.Id), list.Select(static release => release.Id));
    }

    private sealed class FakeTaskRepository : IStockTaskRepository
    {
        public List<StockTask> Items { get; } = [];

        public Task<List<StockTask>> ListAsync() => Task.FromResult(Items.ToList());

        public Task<StockTask?> GetAsync(string identifier) => Task.FromResult(Items.FirstOrDefault(task => task.Id == identifier));

        public Task<StockTask> SaveAsync(StockTask item) => Task.FromResult(item);

        public Task DeleteAsync(string identifier) => Task.CompletedTask;

        public Task SetEnabledAsync(string? identifier, bool enabled) => Task.CompletedTask;

        public Task RenameStoreAsync(string oldName, string newName) => Task.CompletedTask;

        public Task RemoveStoreAsync(string storeName) => Task.CompletedTask;
    }
}
