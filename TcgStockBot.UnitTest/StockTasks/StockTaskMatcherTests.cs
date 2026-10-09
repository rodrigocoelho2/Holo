using TcgStockBot.Library.Enums;
using TcgStockBot.WebApi.Features.StockTasks.Models.Entities;
using TcgStockBot.WebApi.Features.StockTasks.Services;
using TcgStockBot.WebApi.Features.Stores.Services.Adapters;

namespace TcgStockBot.UnitTest.StockTasks;

public class StockTaskMatcherTests
{
    private static readonly StockTask EliteTrainerBox = new()
    {
        Id = "etb",
        Name = "ETB",
        SearchQuery = "elite trainer box",
        RequiredKeywords = ["elite trainer box"],
        ExcludedKeywords = ["sleeves", "acrylic"],
        MaxPrice = 60m,
    };

    private static StoreListing Listing(string title, decimal? price = 54.99m, bool inStock = true) =>
        new("Shop", title, new Uri("https://shop.example/products/etb"), price, inStock);

    [Fact]
    public void Evaluate_EnglishInStockUnderMaxPrice_Matches() =>
        Assert.Equal(MatchOutcome.Match,
            StockTaskMatcher.Evaluate(EliteTrainerBox, Listing("Pokémon ELITE Trainer Box (EN)"), UnmarkedLanguagePolicy.Unknown));

    [Fact]
    public void Evaluate_PriceExactlyMax_Matches() =>
        Assert.Equal(MatchOutcome.Match,
            StockTaskMatcher.Evaluate(EliteTrainerBox, Listing("Elite Trainer Box (EN)", 60m), UnmarkedLanguagePolicy.Unknown));

    [Fact]
    public void Evaluate_AboveMaxPrice_IsRejected() =>
        Assert.Equal(MatchOutcome.AboveMaxPrice,
            StockTaskMatcher.Evaluate(EliteTrainerBox, Listing("Elite Trainer Box (EN)", 89.90m), UnmarkedLanguagePolicy.Unknown));

    [Fact]
    public void Evaluate_Portuguese_IsRejectedWhateverThePolicy()
    {
        foreach (var policy in Enum.GetValues<UnmarkedLanguagePolicy>())
        {
            Assert.Equal(MatchOutcome.NotEnglish, StockTaskMatcher.Evaluate(EliteTrainerBox, Listing("Elite Trainer Box (PT)"), policy));
        }
    }

    [Theory]
    [InlineData(UnmarkedLanguagePolicy.Unknown, MatchOutcome.LanguageUnknown)]
    [InlineData(UnmarkedLanguagePolicy.English, MatchOutcome.Match)]
    [InlineData(UnmarkedLanguagePolicy.Confirm, MatchOutcome.MatchLanguageToConfirm)]
    public void Evaluate_UnmarkedTitle_FollowsStorePolicy(UnmarkedLanguagePolicy policy, MatchOutcome expected) =>
        Assert.Equal(expected, StockTaskMatcher.Evaluate(EliteTrainerBox, Listing("Elite Trainer Box Mega Evolution"), policy));

    [Fact]
    public void Evaluate_UnmarkedTitleOutOfStock_IsOutOfStockEvenWhenConfirming() =>
        Assert.Equal(MatchOutcome.OutOfStock,
            StockTaskMatcher.Evaluate(EliteTrainerBox, Listing("Elite Trainer Box", inStock: false), UnmarkedLanguagePolicy.Confirm));

    [Fact]
    public void Evaluate_MarkedEnglish_IsAPlainMatchEvenWhenConfirming() =>
        Assert.Equal(MatchOutcome.Match,
            StockTaskMatcher.Evaluate(EliteTrainerBox, Listing("Elite Trainer Box (EN)"), UnmarkedLanguagePolicy.Confirm));

    [Fact]
    public void Evaluate_AcrylicCase_IsExcluded() =>
        Assert.Equal(MatchOutcome.ExcludedKeyword,
            StockTaskMatcher.Evaluate(EliteTrainerBox, Listing("The Acrylic Box – Elite Trainer Box Pokémon", 20.99m), UnmarkedLanguagePolicy.English));

    [Fact]
    public void Evaluate_KeywordOnlyAsPartOfAnotherWord_IsNotAMatch() =>
        Assert.Equal(MatchOutcome.MissingKeyword,
            StockTaskMatcher.Evaluate(EliteTrainerBox, Listing("Elite Trainer Boxes Display (EN)"), UnmarkedLanguagePolicy.Unknown));

    private static readonly StockTask OnePieceBoosterBox = new()
    {
        Id = "op-box-en",
        Name = "One Piece Booster Box (EN)",
        SearchQuery = "booster box",
        RequiredKeywords = ["one piece|op*|eb*|prb*", "booster box|booster display"],
        ExcludedKeywords = ["mini|half"],
        MaxPrice = 130m,
    };

    [Theory]
    [InlineData("One Piece - OP15 Booster Box")]                        // prefix "op*" on "op15"
    [InlineData("Royal Blood OP10 Booster Display (24 Packs) - EN")]   // no "one piece", "display" instead of "box"
    [InlineData("OP-16 The Time of Battle Booster Box")]                // "op 16" after normalization
    [InlineData("EB-04 Egghead Crisis Booster Box")]
    public void Alternatives_AnyAlternativeSatisfiesTheKeyword(string title) =>
        Assert.Equal(MatchOutcome.Match, StockTaskMatcher.Evaluate(OnePieceBoosterBox, Listing(title, 120m), UnmarkedLanguagePolicy.English));

    [Theory]
    [InlineData("Naruto Mythos Konoha Shido Booster Box")]   // no alternative of the first keyword
    [InlineData("One Piece Card Game - Starter Deck ST36")]  // no alternative of the second keyword
    public void Alternatives_NoAlternativePresent_IsMissingKeyword(string title) =>
        Assert.Equal(MatchOutcome.MissingKeyword, StockTaskMatcher.Evaluate(OnePieceBoosterBox, Listing(title, 120m), UnmarkedLanguagePolicy.English));

    [Fact]
    public void Alternatives_WorkInExcludedKeywordsToo() =>
        Assert.Equal(MatchOutcome.ExcludedKeyword,
            StockTaskMatcher.Evaluate(OnePieceBoosterBox, Listing("One Piece OP-15 Half Booster Box", 60m), UnmarkedLanguagePolicy.English));

    [Fact]
    public void Prefix_OnlyMatchesTheStartOfAWord() =>
        Assert.Equal(MatchOutcome.MissingKeyword,
            StockTaskMatcher.Evaluate(OnePieceBoosterBox, Listing("Stop Booster Box", 60m), UnmarkedLanguagePolicy.English));   // "stop" ≠ "op*"

    [Fact]
    public void Evaluate_NoPrice_IsRejected() =>
        Assert.Equal(MatchOutcome.NoPrice,
            StockTaskMatcher.Evaluate(EliteTrainerBox, Listing("Elite Trainer Box (EN)", price: null), UnmarkedLanguagePolicy.Unknown));

    private static readonly StockTask JapaneseBoosterBox = new()
    {
        Id = "booster-box-jp",
        Name = "Booster Box (JP)",
        SearchQuery = "booster box",
        RequiredKeywords = ["booster box"],
        MaxPrice = 150m,
        Language = TaskLanguage.Japanese,
    };

    [Theory]
    [InlineData("151 Booster Box JP")]
    [InlineData("Terastal Festival Booster Box – JAPANESE")]
    public void JapaneseTask_JapaneseTitle_Matches(string title) =>
        Assert.Equal(MatchOutcome.Match, StockTaskMatcher.Evaluate(JapaneseBoosterBox, Listing(title, 120m), UnmarkedLanguagePolicy.English));

    [Theory]
    [InlineData("Perfect Order Booster Box")] // unmarked: stores always mark JP, so this is not the JP print run
    [InlineData("Perfect Order Booster Box (EN)")]
    [InlineData("Booster Box (PT)")]
    public void JapaneseTask_NonJapaneseTitle_IsRejectedWhateverTheStorePolicy(string title)
    {
        foreach (var policy in Enum.GetValues<UnmarkedLanguagePolicy>())
        {
            Assert.Equal(MatchOutcome.NotJapanese, StockTaskMatcher.Evaluate(JapaneseBoosterBox, Listing(title, 120m), policy));
        }
    }

    [Fact]
    public void EnglishTask_JapaneseTitle_IsNotEnglish() =>
        Assert.Equal(MatchOutcome.NotEnglish,
            StockTaskMatcher.Evaluate(EliteTrainerBox, Listing("Elite Trainer Box JP"), UnmarkedLanguagePolicy.English));

    [Fact]
    public void Task_WithoutLanguage_DefaultsToEnglish() =>
        Assert.Equal(TaskLanguage.English, new StockTask().Language);

    [Fact]
    public void Task_WithoutGame_DefaultsToPokemon() =>
        Assert.Equal(TcgGame.Pokemon, new StockTask().Game);

    [Theory]
    [InlineData(MatchOutcome.Match, true)]
    [InlineData(MatchOutcome.MatchLanguageToConfirm, true)]
    [InlineData(MatchOutcome.LanguageUnknown, false)]
    [InlineData(MatchOutcome.OutOfStock, false)]
    public void IsMatch_OnlyForAlertingOutcomes(MatchOutcome outcome, bool expected) =>
        Assert.Equal(expected, outcome.IsMatch());
}
