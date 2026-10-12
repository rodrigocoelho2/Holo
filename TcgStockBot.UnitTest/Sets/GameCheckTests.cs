using TcgStockBot.Library.Enums;
using TcgStockBot.Library.Text;
using TcgStockBot.WebApi.Features.Sets.Repositories;
using TcgStockBot.WebApi.Features.Sets.Services;
using TcgStockBot.WebApi.Features.StockTasks.Models.Entities;
using TcgStockBot.WebApi.Features.StockTasks.Services;
using TcgStockBot.WebApi.Features.Stores.Services.Adapters;

namespace TcgStockBot.UnitTest.Sets;

/// <summary>
/// The game check: a generic product (booster box, booster, deck) must name the task's game or one of its sets.
/// </summary>
public sealed class GameCheckTests
{
    private static readonly SetLookup Sets = new(new SetRepository().ListAsync().GetAwaiter().GetResult());

    private static readonly StockTask PokemonDisplay = new()
    {
        Id = "pkm-booster-display",
        Name = "Pokémon Booster Display",
        RequiredKeywords = ["booster display|booster box|display"],
        MaxPrice = 300m,
        Game = TcgGame.Pokemon,
    };

    private static readonly StockTask PokemonEtb = new()
    {
        Id = "pkm-etb",
        Name = "Pokémon Elite Trainer Box",
        RequiredKeywords = ["elite trainer box|etb"],
        MaxPrice = 300m,
        Game = TcgGame.Pokemon,
    };

    private static readonly StockTask MagicDisplay = new()
    {
        Id = "mtg-booster-display",
        Name = "Magic Play Booster Box",
        RequiredKeywords = ["booster display|booster box|display"],
        MaxPrice = 300m,
        Game = TcgGame.Magic,
    };

    private static StoreListing Listing(string title) => new("Shop", title, new Uri("https://shop.example/p"), 120m, true);

    [Theory]
    [InlineData("Flesh & Blood TCG - Bright Lights Booster Display (24 Packs)")]
    [InlineData("Naruto Mythos TCG: First Set Booster Box (24 packs – 2nd Edition)")]
    [InlineData("Sorcery TCG: Contested Realm - Arthurian Legends Booster Display (24 packs) - EN")]
    [InlineData("Radiance Booster Display (24 packs) - EN")]
    public void OtherGames_AreNotPokemon(string title) =>
        Assert.Equal(MatchOutcome.OtherGame, StockTaskMatcher.Evaluate(PokemonDisplay, Listing(title), UnmarkedLanguagePolicy.English, null, Sets));

    [Theory]
    [InlineData("Perfect Order Booster Box")]
    [InlineData("Pokémon Booster Display (36)")]
    [InlineData("PKM ME04 Booster Display")]
    public void TheGameOrASet_Passes(string title) =>
        Assert.Equal(MatchOutcome.Match, StockTaskMatcher.Evaluate(PokemonDisplay, Listing(title), UnmarkedLanguagePolicy.English, null, Sets));

    [Fact]
    public void PokemonOnlyKinds_NeedNoGameName() =>
        Assert.Equal(MatchOutcome.Match, StockTaskMatcher.Evaluate(PokemonEtb, Listing("Delta Reign Elite Trainer Box"), UnmarkedLanguagePolicy.English, null, Sets));

    [Fact]
    public void WithoutSets_ThereIsNoGameCheck() =>
        Assert.Equal(MatchOutcome.Match, StockTaskMatcher.Evaluate(PokemonDisplay, Listing("Flesh & Blood Booster Display"), UnmarkedLanguagePolicy.English));

    [Theory]
    [InlineData("One Piece Card Game - Starter Deck - ST34 [EN]", "st34", "ST-34")]
    [InlineData("One Piece Card Game: Starter Deck Straw Hat Crew ST01 (Inglés)", "st1", "ST-01")]
    [InlineData("ST-01 Straw Hat Crew Starter Deck", "st1", "ST-01")]
    public void StarterDecks_AreTheirCode(string title, string variant, string label)
    {
        var kind = ProductKindClassifier.Classify(title);
        Assert.Equal(variant, ProductKindClassifier.Variant(title, kind, ["one piece"]));
        Assert.Equal(label, ProductKindClassifier.VariantLabel(title, kind, ["one piece"]));
    }

    [Fact]
    public void Store_IsNotADeckCode() =>
        Assert.Null(ProductKindClassifier.DeckCode("Stores best deck 2"));

    [Theory]
    [InlineData("Magic: The Gathering - Edge of Eternities Play Booster Box (30 packs)")]
    [InlineData("MTG Lorwyn Eclipsed Play Booster Display")]
    [InlineData("Secrets of Strixhaven Play Booster Display")]
    public void Magic_TheGameOrASet_Passes(string title) =>
        Assert.Equal(MatchOutcome.Match, StockTaskMatcher.Evaluate(MagicDisplay, Listing(title), UnmarkedLanguagePolicy.English, null, Sets));

    [Theory]
    [InlineData("Perfect Order Booster Box")]
    [InlineData("One Piece OP-15 Booster Box")]
    public void Magic_OtherGamesAreNotMagic(string title) =>
        Assert.Equal(MatchOutcome.OtherGame, StockTaskMatcher.Evaluate(MagicDisplay, Listing(title), UnmarkedLanguagePolicy.English, null, Sets));

    [Fact]
    public void Magic_IsNotPokemon() =>
        Assert.Equal(MatchOutcome.OtherGame, StockTaskMatcher.Evaluate(PokemonDisplay, Listing("Magic: The Gathering - The Hobbit Play Booster Box"), UnmarkedLanguagePolicy.English, null, Sets));

    [Theory]
    [InlineData("Magic: The Gathering - The Hobbit Play Booster Box", "mtg-hob")]
    [InlineData("MTG Teenage Mutant Ninja Turtles Collector Booster Display", "mtg-tmt")]
    [InlineData("Magic Marvel's Spider-Man Bundle", "mtg-spm")]
    [InlineData("Magic: The Gathering | Marvel Super Heroes Play Booster Display", "mtg-msh")]
    public void MagicSets_AreFound(string title, string id) =>
        Assert.Equal(id, Sets.Find(TcgGame.Magic, title)?.Id);
}
