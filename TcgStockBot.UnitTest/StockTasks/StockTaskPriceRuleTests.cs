using TcgStockBot.Library.Enums;
using TcgStockBot.WebApi.Features.Msrps.Models.Entities;
using TcgStockBot.WebApi.Features.Msrps.Services;
using TcgStockBot.WebApi.Features.StockTasks.Models.Entities;
using TcgStockBot.WebApi.Features.StockTasks.Services;
using TcgStockBot.WebApi.Features.Stores.Services.Adapters;

namespace TcgStockBot.UnitTest.StockTasks;

/// <summary>
/// A task per set ("Chaos Rising"): each product kind is judged against its own MSRP, and the task can keep only
/// some kinds.
/// </summary>
public class StockTaskPriceRuleTests
{
    private static readonly MsrpLookup Msrp = new([
        new MsrpEntry { Kind = "etb", Price = 59.99m },
        new MsrpEntry { Kind = "booster-pack", Price = 5.99m },
        new MsrpEntry { Kind = "booster-display", Price = 179.99m },
    ]);

    private static StockTask SetTask(decimal maxPrice = 0m, int tolerance = 10, params string[] kinds) => new()
    {
        Id = "chaos-rising",
        Name = "Pokémon Chaos Rising",
        SearchQuery = "chaos rising",
        RequiredKeywords = ["chaos rising"],
        MaxPrice = maxPrice,
        PriceRule = PriceRule.Msrp,
        MsrpTolerancePercent = tolerance,
        Kinds = [.. kinds],
    };

    private static StoreListing Listing(string title, decimal price) =>
        new("Shop", title, new Uri("https://shop.example/p"), price, true);

    [Theory]
    [InlineData("Chaos Rising Elite Trainer Box (EN)", 65.99, MatchOutcome.Match)]          // 59,99 + 10% = 65,99
    [InlineData("Chaos Rising Elite Trainer Box (EN)", 66.00, MatchOutcome.AboveMaxPrice)]
    [InlineData("Chaos Rising Booster (EN)", 5.49, MatchOutcome.Match)]
    [InlineData("Chaos Rising Booster (EN)", 7.50, MatchOutcome.AboveMaxPrice)]             // 5,99 + 10% = 6,59
    [InlineData("Chaos Rising Booster Display (EN)", 189.00, MatchOutcome.Match)]
    public void Evaluate_EachKindAgainstItsOwnMsrp(string title, double price, MatchOutcome expected) =>
        Assert.Equal(expected, StockTaskMatcher.Evaluate(SetTask(), Listing(title, (decimal)price), UnmarkedLanguagePolicy.Unknown, Msrp));

    [Fact]
    public void Evaluate_KindWithoutMsrp_UsesTheFixedMaximum() =>
        Assert.Equal(MatchOutcome.Match,
            StockTaskMatcher.Evaluate(SetTask(maxPrice: 30m), Listing("Chaos Rising Mini Tin (EN)", 14.99m), UnmarkedLanguagePolicy.Unknown, Msrp));

    [Fact]
    public void Evaluate_KindWithoutMsrpAndNoFixedMaximum_IsNoMsrp() =>
        Assert.Equal(MatchOutcome.NoMsrp,
            StockTaskMatcher.Evaluate(SetTask(), Listing("Chaos Rising Mini Tin (EN)", 14.99m), UnmarkedLanguagePolicy.Unknown, Msrp));

    [Fact]
    public void Evaluate_NegativeTolerance_WantsADiscount() =>
        Assert.Equal(MatchOutcome.AboveMaxPrice,
            StockTaskMatcher.Evaluate(SetTask(tolerance: -10), Listing("Chaos Rising Elite Trainer Box (EN)", 55m), UnmarkedLanguagePolicy.Unknown, Msrp));

    [Fact]
    public void Evaluate_KindNotWanted_IsOtherKind_AndNotRelevant()
    {
        var outcome = StockTaskMatcher.Evaluate(SetTask(0m, 10, "etb", "booster-display"), Listing("Chaos Rising Booster (EN)", 4.99m), UnmarkedLanguagePolicy.Unknown, Msrp);

        Assert.Equal(MatchOutcome.OtherKind, outcome);
        Assert.False(outcome.IsRelevant());
    }

    [Fact]
    public void Evaluate_KindWanted_Matches() =>
        Assert.Equal(MatchOutcome.Match,
            StockTaskMatcher.Evaluate(SetTask(0m, 10, "etb"), Listing("Chaos Rising ETB (EN)", 59.99m), UnmarkedLanguagePolicy.Unknown, Msrp));

    [Fact]
    public void PriceFor_ReportsKindMsrpAndLimit()
    {
        var check = StockTaskMatcher.PriceFor(SetTask(), "Chaos Rising Elite Trainer Box", Msrp);

        Assert.Equal("etb", check.Kind.Code);
        Assert.Equal(59.99m, check.Msrp?.Price);
        Assert.Equal(65.99m, check.Limit);
    }

    [Fact]
    public void PriceFor_FixedRule_IgnoresTheMsrp()
    {
        var task = SetTask(maxPrice: 200m);
        task.PriceRule = PriceRule.MaxPrice;

        Assert.Equal(200m, StockTaskMatcher.PriceFor(task, "Chaos Rising Booster", Msrp).Limit);
    }
}
