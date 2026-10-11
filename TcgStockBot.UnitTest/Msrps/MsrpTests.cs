using TcgStockBot.WebApi.Features.Alerts.Models;
using TcgStockBot.WebApi.Features.Msrps.Models.Entities;
using TcgStockBot.WebApi.Features.Msrps.Repositories;
using TcgStockBot.WebApi.Features.Msrps.Services;
using TcgStockBot.WebApi.Features.Notifications.Services;
using TcgStockBot.WebApi.Features.Stores.Services.Adapters;

namespace TcgStockBot.UnitTest.Msrps;

/// <summary>
/// MSRP reference prices: one price per kind of product for the game, never borrowed by another game.
/// </summary>
public sealed class MsrpTests : IDisposable
{
    private static readonly MsrpLookup Lookup = new([
        new MsrpEntry { Kind = "etb", Price = 59.99m },
        new MsrpEntry { Kind = "booster-bundle", Price = 35.99m },
    ]);

    private readonly TestDataContext _data = new();

    public void Dispose() => _data.Dispose();

    [Theory]
    [InlineData("Pokémon TCG: Mega Evolution 5 - Pitch Black Elite Trainer Box", null, 59.99)]
    [InlineData("Pitch Black Booster Bundle", "Pokémon Pitch Black (EN)", 35.99)]   // the game comes from the task name
    public void Find_UsesThePriceOfTheKind(string title, string? context, double expected) =>
        Assert.Equal((decimal)expected, Lookup.Find(title, context)!.Price);

    [Theory]
    [InlineData("One Piece OP-09 Booster Bundle", "One Piece OP-09")]       // another game: no Pokémon price
    [InlineData("Riftbound Origins Elite Trainer Box", "Riftbound")]
    [InlineData("Pokémon Pitch Black Card Sleeves", "Pokémon Pitch Black (EN)")]   // no kind
    [InlineData("Pokémon Pitch Black Mini Tin", "Pokémon Pitch Black (EN)")]       // kind without a price
    public void Find_ReturnsNothing_ForOtherGamesAndUnknownKinds(string title, string context) =>
        Assert.Null(Lookup.Find(title, context));

    [Fact]
    public async Task Defaults_HaveOnePricePerKind()
    {
        var defaults = await new MsrpRepository(_data.Context).ListAsync();

        Assert.NotEmpty(defaults);
        Assert.All(defaults, static entry => Assert.True(entry.Price > 0));
        Assert.Equal(defaults.Count, defaults.Select(static entry => $"{entry.Game}|{entry.Kind}").Distinct().Count());
        Assert.Contains(defaults, static entry => entry.Kind == "etb");
    }

    [Fact]
    public async Task DataFile_ReplacesTheDefaults()
    {
        await File.WriteAllTextAsync(_data.Context.MsrpPath, """[ { "game": "Pokémon", "kind": "etb", "price": 49.5 } ]""");

        var lookup = await new MsrpService(new MsrpRepository(_data.Context)).LookupAsync();

        Assert.Equal(49.5m, lookup.Find("Pokémon Pitch Black Elite Trainer Box")!.Price);
        Assert.Null(lookup.Find("Pokémon Pitch Black Booster Bundle"));
    }

    [Theory]
    [InlineData(54.99, 59.99, " · MSRP €59.99 (−8%)")]
    [InlineData(69.99, 59.99, " · MSRP €59.99 (+17%)")]
    [InlineData(59.99, 59.99, " · MSRP €59.99 (=)")]
    public void AlertText_ShowsTheDifferenceToMsrp(double price, double msrp, string expected)
    {
        var alert = new StockAlert("t", "Task", new StoreListing("Loja", "Pitch Black ETB", new Uri("https://loja.example/p"), (decimal)price, true), false, Msrp: (decimal)msrp);

        Assert.Equal(expected.Replace(" ", " "), AlertMessageFormatter.MsrpPart(alert).Replace(" ", " "));
    }

    [Fact]
    public void AlertText_WithoutMsrp_IsUnchanged()
    {
        var alert = new StockAlert("t", "Task", new StoreListing("Loja", "Pitch Black ETB", new Uri("https://loja.example/p"), 54.99m, true), false);

        Assert.Equal(string.Empty, AlertMessageFormatter.MsrpPart(alert));
    }
}
