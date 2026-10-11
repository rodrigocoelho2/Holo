using TcgStockBot.Library.Enums;
using TcgStockBot.WebApi.Features.Compares.Services;
using TcgStockBot.WebApi.Features.PriceHistories.Models.Entities;
using TcgStockBot.WebApi.Features.StockTasks.Services;

namespace TcgStockBot.UnitTest.Compares;

/// <summary>
/// Catalogue helpers: the price a listing had at a moment (for "Mov. 30d") and the words of the new games.
/// </summary>
public class CatalogueTests
{
    private static readonly DateTimeOffset Start = new(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);

    private static PriceHistoryEntry Entry(params (int Day, decimal Price)[] points) => new()
    {
        Points = points.Select(static point => new PricePoint { At = Start.AddDays(point.Day), Price = point.Price, InStock = true }).ToList(),
    };

    [Fact]
    public void PriceAt_IsTheLastPointBeforeTheMoment()
    {
        var entry = Entry((0, 80m), (10, 70m), (20, 65m));

        Assert.Equal(70m, CompareService.PriceAt(entry, Start.AddDays(15)));
        Assert.Equal(65m, CompareService.PriceAt(entry, Start.AddDays(40)));
    }

    [Fact]
    public void PriceAt_BeforeTheFirstPoint_IsTheFirstPrice()
    {
        Assert.Equal(80m, CompareService.PriceAt(Entry((5, 80m)), Start));
        Assert.Null(CompareService.PriceAt(Entry(), Start));
    }

    [Theory]
    [InlineData(TcgGame.Riftbound, "Riftbound")]
    [InlineData(TcgGame.Cyberpunk, "Cyberpunk")]
    public void NewGames_HaveTheirWords(TcgGame game, string words) =>
        Assert.Equal(words, StockTaskMatcher.GameWords(game));
}
