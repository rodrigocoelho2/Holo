using TcgStockBot.Library.Enums;
using TcgStockBot.Library.Text;

namespace TcgStockBot.UnitTest.Library;

public class LanguageClassifierTests
{
    [Theory]
    [InlineData("Pokémon TCG: Elite Trainer Box (EN)")]
    [InlineData("Pokemon Elite Trainer Box [EN] Mega Evolution")]
    [InlineData("Elite Trainer Box Destined Rivals - English")]
    [InlineData("Caixa de Treinador de Elite - Versão Inglesa")]
    [InlineData("Pokémon Booster Bundle Inglês")]
    [InlineData("Pokémon Booster Bundle EN")]
    public void Classify_EnglishMarkers_ReturnsEnglish(string title) =>
        Assert.Equal(ProductLanguage.English, LanguageClassifier.Classify(title));

    [Theory]
    [InlineData("Pokémon TCG: Elite Trainer Box (PT)")]
    [InlineData("Caixa de Treinador de Elite - Português")]
    [InlineData("Pokémon Elite Trainer Box Español")]
    [InlineData("Pokémon Elite Trainer Box DE")]
    [InlineData("Pokémon Elite Trainer Box versão espanhola")]
    [InlineData("Pokémon Mega Inferno X Booster Box (KOR)")]
    [InlineData("Dark Crystal Blazing Slim Booster Box (CH)")]
    [InlineData("Pokemon Glaceon GX Gift Box (CN)")]
    [InlineData("Riftbound Origins Booster Box (Chinese, Slim)")]
    public void Classify_OtherLanguageMarkers_ReturnsNonEnglish(string title) =>
        Assert.Equal(ProductLanguage.NonEnglish, LanguageClassifier.Classify(title));

    [Theory]
    [InlineData("Pokemon Booster Box [JP] Terastal Festival")]
    [InlineData("151 Booster Pack JP")]
    [InlineData("Pokémon Card 151 Booster Box – JAPANESE – BREAK")]
    [InlineData("POKEMON 151 BOOSTER BOX JP")] // all-caps title: "JP" still counts as a word
    [InlineData("Pokémon Booster Box Japonês")]
    [InlineData("ポケモンカードゲーム 拡張パック テラスタルフェスex")]
    public void Classify_JapaneseMarkers_ReturnsJapanese(string title) =>
        Assert.Equal(ProductLanguage.Japanese, LanguageClassifier.Classify(title));

    [Fact]
    public void Classify_BothEnglishAndJapaneseMarkers_ReturnsUnknown() =>
        Assert.Equal(ProductLanguage.Unknown, LanguageClassifier.Classify("Booster Box (EN) (JP)"));

    [Theory]
    [InlineData("Pokémon TCG Elite Trainer Box Mega Evolution")]
    [InlineData("Caixa de Treinador de Elite")] // "de" is a Portuguese preposition, not German
    [InlineData("POKEMON ELITE TRAINER BOX DE LUXO")] // all-caps title: bare codes are ignored
    [InlineData("Elite Trainer Box (EN/PT)")]
    public void Classify_NoTrustworthyMarker_ReturnsUnknown(string title) =>
        Assert.Equal(ProductLanguage.Unknown, LanguageClassifier.Classify(title));

    [Fact]
    public void Classify_BothEnglishAndPortugueseMarkers_ReturnsUnknown() =>
        Assert.Equal(ProductLanguage.Unknown, LanguageClassifier.Classify("Elite Trainer Box (EN) (PT)"));
}
