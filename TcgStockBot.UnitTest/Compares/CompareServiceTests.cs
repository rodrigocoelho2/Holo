using TcgStockBot.Library.Services;
using TcgStockBot.WebApi.Extensions.Globalization;
using TcgStockBot.WebApi.Features.Compares.Models.DTOs;
using TcgStockBot.WebApi.Features.Compares.Repositories;
using TcgStockBot.WebApi.Features.Compares.Services;
using TcgStockBot.WebApi.Features.Msrps.Repositories;
using TcgStockBot.WebApi.Features.Msrps.Services;
using TcgStockBot.WebApi.Features.PriceHistories.Models.Entities;
using TcgStockBot.WebApi.Features.PriceHistories.Repositories;
using TcgStockBot.WebApi.Features.PriceHistories.Services;
using TcgStockBot.WebApi.Features.Releases.Models.Entities;
using TcgStockBot.WebApi.Features.Releases.Repositories;
using TcgStockBot.WebApi.Features.Sets.Repositories;
using TcgStockBot.WebApi.Features.Sets.Services;
using TcgStockBot.Library.Enums;
using TcgStockBot.WebApi.Features.StockTasks.Models.Entities;
using TcgStockBot.WebApi.Features.StockTasks.Repositories;

namespace TcgStockBot.UnitTest.Compares;

/// <summary>
/// Compare grouping: game + set + kind of product, variants kept apart, manual corrections kept across calls.
/// </summary>
public sealed class CompareServiceTests : IDisposable
{
    private readonly TestDataContext _data = new();
    private readonly PriceHistoryService _history;
    private readonly FakeTaskRepository _tasks = new();
    private readonly CompareService _service;
    private readonly ReleaseRepository _releases;

    public CompareServiceTests()
    {
        _history = new PriceHistoryService(new PriceHistoryRepository(_data.Context), new GlobalizationHelper(), TimeProvider.System);
        _releases = new ReleaseRepository(_data.Context);
        _service = new CompareService(_history, _tasks, new CompareOverrideRepository(_data.Context), new MsrpService(new MsrpRepository(_data.Context)),
            new SetService(new SetRepository(), _releases), new GlobalizationHelper());
        _tasks.Items.Add(new StockTask { Id = "set-pitch-black", Name = "Pokémon Pitch Black (EN)", RequiredKeywords = ["pitch black"] });
        _tasks.Items.Add(new StockTask { Id = "set-chaos-rising", Name = "Pokémon Chaos Rising (EN)", RequiredKeywords = ["chaos rising"] });
        _tasks.Items.Add(new StockTask { Id = "pkm-etb", Name = "Pokémon Elite Trainer Box", RequiredKeywords = ["elite trainer box|etb"] });
        _tasks.Items.Add(new StockTask { Id = "op-box", Name = "One Piece Booster Box", Game = TcgGame.OnePiece, RequiredKeywords = ["one piece", "booster box|display"] });
    }

    public void Dispose() => _data.Dispose();

    private static PriceObservation Seen(string store, string title, decimal price, bool inStock = true, string task = "set-pitch-black") =>
        new(store, title, $"https://{store.Replace(" ", string.Empty).ToLowerInvariant()}.example/products/{title.GetHashCode():x}", null, "EUR", price, inStock, task);

    private async Task<List<CompareGroupDTO>> ListAsync() => (await _service.ListAsync()).Result!;

    [Fact]
    public async Task SameKindOfTheSameSet_IsOneProduct_WhateverTheWording()
    {
        await _history.RecordAsync([
            Seen("Loja A", "Pitch Black Elite Trainer Box", 69.99m, inStock: false),
            Seen("Loja B", "Pokémon TCG: Mega Evolution 5 - Pitch Black Elite Trainer Box", 79.90m),
            Seen("Loja C", "PKM - Mega Evolution 5 - Pitch Black Elite Trainer Box", 80m),
            Seen("Loja B", "Pokémon TCG: Mega Evolution 5 - Pitch Black Booster Display (36 Booster)", 249.90m),
            Seen("Loja A", "Chaos Rising Elite Trainer Box", 80m, task: "set-chaos-rising"),
        ]);

        var groups = await ListAsync();

        var etb = Assert.Single(groups, static group => group.Title == "Pitch Black · Elite Trainer Box");
        Assert.Equal(["Loja B", "Loja C", "Loja A"], etb.Listings.Select(static listing => listing.StoreName));   // in stock first, then cheapest
        Assert.Contains(groups, static group => group.Title == "Pitch Black · Booster Display");
        Assert.Contains(groups, static group => group.Title == "Chaos Rising · Elite Trainer Box");
        Assert.Empty(etb.Listings.SelectMany(static listing => listing.Points));   // the list has no points
    }

    [Fact]
    public async Task Variants_AreDifferentProducts()
    {
        await _history.RecordAsync([
            Seen("Loja A", "Pitch Black Premium Checklane Blister - Luxray", 11.90m),
            Seen("Loja B", "Pokémon TCG: Pitch Black Premium Checklane Blister Luxray", 10.90m),
            Seen("Loja A", "Pitch Black Premium Checklane Blister - Gengar", 16.90m),
        ]);

        var groups = await ListAsync();

        Assert.Equal(2, Assert.Single(groups, static group => group.Title == "Pitch Black · Checklane Blister – Premium Luxray").Listings.Count);
        Assert.Single(Assert.Single(groups, static group => group.Title == "Pitch Black · Checklane Blister – Premium Gengar").Listings);
    }

    [Fact]
    public async Task Get_ReturnsThePoints_AndTheLastDrop()
    {
        await _history.RecordAsync([Seen("Loja B", "Pitch Black Elite Trainer Box", 99.90m)]);
        await _history.RecordAsync([Seen("Loja B", "Pitch Black Elite Trainer Box", 89.90m)]);
        await _history.RecordAsync([Seen("Loja B", "Pitch Black Elite Trainer Box", 95m)]);

        var group = (await _service.GetAsync("set:pkm-me05|etb|")).Result!;

        var listing = Assert.Single(group.Listings);
        Assert.Equal(3, listing.Points.Count);
        Assert.Equal(99.90m, listing.LastDropFrom);
        Assert.Equal(89.90m, listing.LastDropTo);
        Assert.Equal(89.90m, listing.LowestPrice);
        Assert.Equal(95m, listing.CurrentPrice);
    }

    [Fact]
    public async Task Separate_ThenJoin_ThenBackToAutomatic()
    {
        await _history.RecordAsync([
            Seen("Loja A", "Pitch Black Elite Trainer Box", 69.99m),
            Seen("Loja B", "Pitch Black ETB Pokémon Center edition", 119.90m),   // automatically: "Pokémon Center ETB"
        ]);
        var pokemonCenter = (await ListAsync()).Single(static group => group.Title.Contains("Pokémon Center")).Listings.Single().HistoryKey;

        // Join it to the regular ETB…
        Assert.Equal(ServiceApiResponseStatus.Success, (await _service.AssignAsync(new CompareAssignRequest { HistoryKey = pokemonCenter, GroupKey = "set:pkm-me05|etb|" })).Status);
        var etb = (await ListAsync()).Single(static group => group.Title == "Pitch Black · Elite Trainer Box");
        Assert.Equal(2, etb.Listings.Count);
        Assert.True(etb.Listings.Single(listing => listing.HistoryKey == pokemonCenter).IsManual);

        // …separate it as a product of its own…
        await _service.AssignAsync(new CompareAssignRequest { HistoryKey = pokemonCenter, GroupKey = "solo" });
        var groups = await ListAsync();
        Assert.Single(groups.Single(static group => group.Title == "Pitch Black · Elite Trainer Box").Listings);
        Assert.Contains(groups, group => group.Key == $"solo|{pokemonCenter}" && group.Title == "Pitch Black ETB Pokémon Center edition");

        // …and back to automatic.
        await _service.AssignAsync(new CompareAssignRequest { HistoryKey = pokemonCenter, GroupKey = string.Empty });
        Assert.Contains(await ListAsync(), static group => group.Title == "Pitch Black · Pokémon Center ETB");
    }

    [Fact]
    public async Task ListingsOfDeletedTasks_AreLeftOut()
    {
        await _history.RecordAsync([Seen("Loja A", "Pitch Black Elite Trainer Box", 69.99m), Seen("Loja A", "Old product", 9m, task: "deleted-task")]);

        var group = Assert.Single(await ListAsync());

        Assert.Equal("Pitch Black · Elite Trainer Box", group.Title);
    }

    [Fact]
    public async Task Assign_ValidatesTheListingAndTheTarget()
    {
        await _history.RecordAsync([Seen("Loja A", "Pitch Black Elite Trainer Box", 69.99m)]);
        var key = (await ListAsync()).Single().Listings.Single().HistoryKey;

        Assert.Equal(ServiceApiResponseStatus.ValidationProblems, (await _service.AssignAsync(new CompareAssignRequest { HistoryKey = "loja|https://nope.example/x", GroupKey = "solo" })).Status);
        Assert.Equal(ServiceApiResponseStatus.ValidationProblems, (await _service.AssignAsync(new CompareAssignRequest { HistoryKey = key, GroupKey = "unknown-task|etb|" })).Status);
        Assert.Equal(ServiceApiResponseStatus.ValidationProblems, (await _service.AssignAsync(new CompareAssignRequest { HistoryKey = key, GroupKey = "set:no-such-set|etb|" })).Status);
        Assert.Equal(ServiceApiResponseStatus.NotFound, (await _service.GetAsync("set-pitch-black|tin|nothing")).Status);
    }

    [Fact]
    public async Task SameProductSeenByTwoTasks_KeepsBothTaskIds()
    {
        await _history.RecordAsync([
            Seen("Loja A", "Pitch Black Elite Trainer Box", 69.99m),
            Seen("Loja A", "Pitch Black Elite Trainer Box", 69.99m, task: "pkm-etb-en"),
        ]);

        var entry = Assert.Single(await _history.SnapshotAsync()).Value;

        Assert.Equal(["set-pitch-black", "pkm-etb-en"], entry.StockTaskIds);
        Assert.Single(entry.Points);
    }

    [Fact]
    public async Task OneTaskForEverySet_GroupsByTheSetInTheTitle()
    {
        await _history.RecordAsync([
            Seen("Loja A", "Pitch Black ETB", 59.99m, task: "pkm-etb"),
            Seen("Loja B", "Pokémon TCG: Mega Evolution 5 - Pitch Black Elite Trainer Box", 62m, task: "pkm-etb"),
            Seen("Loja C", "PKM ME05 Pitch Black Elite Trainer Box [MAX 1 POR PESSOA] - Inglés", 64m, task: "pkm-etb"),
            Seen("Loja A", "Chaos Rising Elite Trainer Box", 58m, task: "pkm-etb"),
            Seen("Loja D", "Pitch Black Elite Trainer Box", 61m),   // the set task finds the same product
        ]);

        var groups = await ListAsync();

        var pitchBlack = Assert.Single(groups, static group => group.SetName == "Pitch Black");
        Assert.Equal("Pitch Black · Elite Trainer Box", pitchBlack.Title);
        Assert.Equal(4, pitchBlack.Listings.Count);
        Assert.Equal("pkm-me05", pitchBlack.SetId);
        Assert.Equal("ME05", pitchBlack.SetCode);
        Assert.Equal(new DateOnly(2026, 7, 17), pitchBlack.ReleaseDate);
        Assert.Equal(TcgGame.Pokemon, pitchBlack.Game);
        Assert.Single(Assert.Single(groups, static group => group.SetName == "Chaos Rising").Listings);
    }

    [Fact]
    public async Task SetCodes_AndTheGameOfTheTask_FindTheSet()
    {
        await _history.RecordAsync([
            Seen("Loja A", "One Piece Card Game OP-13 Booster Box (EN)", 99.90m, task: "op-box"),
            Seen("Loja B", "OP13 Carrying on His Will Booster Display", 104m, task: "op-box"),
        ]);

        var group = Assert.Single(await ListAsync());

        Assert.Equal("Carrying on His Will · Booster Display", group.Title);
        Assert.Equal(TcgGame.OnePiece, group.Game);
        Assert.Equal(2, group.Listings.Count);
    }

    [Fact]
    public async Task UnknownSet_IsGroupedByItsTask()
    {
        await _history.RecordAsync([Seen("Loja A", "Delta Reign Elite Trainer Box", 59.99m, task: "pkm-etb")]);

        var group = Assert.Single(await ListAsync());

        Assert.StartsWith("pkm-etb|etb|", group.Key);
        Assert.Null(group.SetId);
    }

    [Fact]
    public async Task Releases_AddSets_AndAliasesToBuiltInSets()
    {
        await _releases.SaveAsync([
            new Release { Id = "r1", Name = "Pokémon — Delta Reign", Game = TcgGame.Pokemon, ReleaseDate = new DateOnly(2026, 11, 13), Aliases = ["ME06"] },
            new Release { Id = "r2", Name = "Pokémon — Pitch Black", Game = TcgGame.Pokemon, ReleaseDate = new DateOnly(2026, 7, 17), Aliases = ["Escuridao Total"] },
        ]);
        await _history.RecordAsync([
            Seen("Loja A", "PKM ME06 Elite Trainer Box", 59.99m, task: "pkm-etb"),
            Seen("Loja B", "Escuridão Total Elite Trainer Box", 61m, task: "pkm-etb"),
        ]);

        var groups = await ListAsync();

        var delta = Assert.Single(groups, static group => group.SetName == "Delta Reign");
        Assert.Equal("set:rel-r1|etb|", delta.Key);
        Assert.Equal(new DateOnly(2026, 11, 13), delta.ReleaseDate);
        Assert.Equal("set:pkm-me05|etb|", Assert.Single(groups, static group => group.SetName == "Pitch Black").Key);
    }

    /// <summary>An in-memory task list.</summary>
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
