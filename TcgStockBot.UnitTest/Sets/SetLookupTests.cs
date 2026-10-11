using TcgStockBot.Library.Enums;
using TcgStockBot.WebApi.Features.Sets.Repositories;
using TcgStockBot.WebApi.Features.Sets.Services;
using TcgStockBot.WebApi.Features.StockTasks.Models.Entities;

namespace TcgStockBot.UnitTest.Sets;

/// <summary>
/// Finding the set a product title names, with the built-in set list.
/// </summary>
public sealed class SetLookupTests
{
    private static readonly SetLookup Lookup = new(new SetRepository().ListAsync().GetAwaiter().GetResult());

    [Theory]
    [InlineData("Pokémon TCG: Mega Evolution 5 - Pitch Black Elite Trainer Box", "pkm-me05")]
    [InlineData("PKM ME05 Booster Bundle", "pkm-me05")]
    [InlineData("Mega Evolution - Phantasmal Flames Booster Display", "pkm-me02")]
    [InlineData("Mega Evolution Elite Trainer Box", "pkm-me01")]
    [InlineData("Scarlet & Violet 151 Ultra Premium Collection", "pkm-sv035")]
    [InlineData("Scarlet & Violet - Prismatic Evolutions Super Premium Collection", "pkm-sv085")]
    [InlineData("Black Bolt Elite Trainer Box", "pkm-sv105-black")]
    [InlineData("30th Celebrations Elite Trainer Box", "pkm-30th")]
    public void Pokemon(string title, string expected) =>
        Assert.Equal(expected, Lookup.Find(TcgGame.Pokemon, title)?.Id);

    [Theory]
    [InlineData("One Piece Card Game OP-13 Booster Box (EN)", "op-op13")]
    [InlineData("One Piece OP14 Booster Display", "op-op14")]
    [InlineData("One Piece - The Azure Sea's Seven Booster Box", "op-op14")]
    [InlineData("One Piece EB-02 Anime 25th Collection Booster Box", "op-eb02")]
    public void OnePiece(string title, string expected) =>
        Assert.Equal(expected, Lookup.Find(TcgGame.OnePiece, title)?.Id);

    [Theory]
    [InlineData("Riftbound Spiritforged Booster Display", "rb-sfd")]
    [InlineData("Riftbound: Origins - Proving Grounds", "rb-ogs")]
    [InlineData("Riftbound Origins Booster", "rb-ogn")]
    public void Riftbound(string title, string expected) =>
        Assert.Equal(expected, Lookup.Find(TcgGame.Riftbound, title)?.Id);

    [Fact]
    public void OnlyTheSetsOfTheGameCount() =>
        Assert.Null(Lookup.Find(TcgGame.OnePiece, "Pitch Black Elite Trainer Box"));

    [Fact]
    public void TitleWithoutASet_HasNone() =>
        Assert.Null(Lookup.Find(TcgGame.Pokemon, "Pokémon Elite Trainer Box"));

    [Fact]
    public void ATaskAboutOneSet_HasThatSet()
    {
        Assert.Equal("pkm-me05", Lookup.ForTask(new StockTask { Id = "t", Name = "Pokémon Pitch Black (EN)" })?.Id);
        Assert.Null(Lookup.ForTask(new StockTask { Id = "t", Name = "Pokémon Elite Trainer Box" }));
    }

    [Fact]
    public void BuiltInSets_HaveUniqueIds_AndAName()
    {
        var sets = new SetRepository().ListAsync().GetAwaiter().GetResult();

        Assert.Equal(sets.Count, sets.Select(static set => set.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(sets, static set => Assert.False(string.IsNullOrWhiteSpace(set.Name)));
    }

    [Theory]
    [InlineData("Pokémon — Delta Reign", "Delta Reign")]
    [InlineData("One Piece OP-15", "OP-15")]
    [InlineData("Riftbound: Unleashed", "Unleashed")]
    [InlineData("Pitch Black", "Pitch Black")]
    public void ReleaseNames_LoseTheGame(string release, string expected) =>
        Assert.Equal(expected, SetService.SetName(release));
}
