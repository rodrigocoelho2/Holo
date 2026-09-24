using TcgStockBot.Library.Enums;
using TcgStockBot.Library.Text;

namespace TcgStockBot.UnitTest.Library;

public class PriceParserTests
{
    [Theory]
    [InlineData("54,99 €", "54.99")]
    [InlineData("€54.99", "54.99")]
    [InlineData("1.234,50€", "1234.50")]
    [InlineData("1,234.50 EUR", "1234.50")]
    [InlineData("PVP: 59,9 €", "59.9")]
    [InlineData("60 €", "60")]
    [InlineData("1.234 €", "1234")]
    [InlineData("54,99.", "54.99")]
    public void Parse_KnownFormats(string text, string expected) =>
        Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), PriceParser.Parse(text));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Esgotado")]
    public void Parse_NoNumber_ReturnsNull(string? text) =>
        Assert.Null(PriceParser.Parse(text));
}
