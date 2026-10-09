using TcgStockBot.Library.Text;

namespace TcgStockBot.UnitTest.Library;

/// <summary>
/// Product kinds from real store titles: the same product must land in the same kind whatever the wording.
/// </summary>
public class ProductKindClassifierTests
{
    [Theory]
    [InlineData("Pitch Black Elite Trainer Box", "etb")]
    [InlineData("Pokémon TCG: Mega Evolution 5 - Pitch Black Elite Trainer Box", "etb")]
    [InlineData("PKM - Mega Evolution 5 - Pitch Black Elite Trainer Box", "etb")]
    [InlineData("Mega Evolution 5 - Pitch Black Elite Trainer Box - EN", "etb")]
    [InlineData("Acrylic Box Pokemon Elite Trainer Box (ETB)", "etb")]
    [InlineData("30th Celebrations Elite Trainer Box [MAX 1 POR PESSOA]", "etb")]
    [InlineData("Pitch Black Booster Box Elite Trainer Box Bundle", "etb-booster-box-combo")]
    [InlineData("Pokémon Center Elite Trainer Box Pitch Black", "pokemon-center-etb")]
    [InlineData("Pitch Black Booster Bundle – MAX 1", "booster-bundle")]
    [InlineData("Pokémon TCG: Mega Evolution 5 - Pitch Black Booster Display (36 Booster)", "booster-display")]
    [InlineData("Perfect Order Booster Box", "booster-display")]
    [InlineData("Mega Evolution - Perfect Order - Booster Display (36 Booster) - EN", "booster-display")]
    [InlineData("Pokémon TCG: Mega Evolution 5 - Pitch Black Half Booster Display (18 Booster)", "half-display")]
    [InlineData("Pitch Black Meia Booster Box [Envio Selado]", "half-display")]
    [InlineData("Pitch Black Booster Box (18 Boosters) – MAX 1", "half-display")]
    [InlineData("Pitch Black Booster Box – MAX 1", "booster-display")]
    [InlineData("Pokémon TCG: Mega Evolution 5 - Pitch Black Booster", "booster-pack")]
    [InlineData("Chaos Rising Booster Pack", "booster-pack")]
    [InlineData("Phantasmal Flames Sleeved Booster Pack", "sleeved-booster")]
    [InlineData("Pokémon TCG: Mega Evolution 5 - Pitch Black 3-pack blister - Binacle", "blister-3")]
    [InlineData("Chaos Rising 3 Pack Blister Charmeleon", "blister-3")]
    [InlineData("Pokémon TCG: Mega Evolution 5 - Pitch Black Premium Checklane Blister - Luxray", "checklane-blister")]
    [InlineData("PKM - Mega Evolutions - Enhanced 2-Pack Blister - Oddish", "blister-2")]
    [InlineData("30th Celebration Mini Tin (Random) - EN", "mini-tin")]
    [InlineData("30th Celebration: Greninja ex Box - EN", "ex-box")]
    [InlineData("30th Celebration Binder Collection - EN", "binder-collection")]
    [InlineData("30th Celebration Poster Collection - EN", "poster-collection")]
    [InlineData("Destined Rivals Ultra Premium Collection", "ultra-premium")]
    [InlineData("Prismatic Evolutions Super Premium Collection", "super-premium")]
    [InlineData("Journey Together Build & Battle Box", "build-battle")]
    public void Classify_RecognisesTheKind(string title, string expected) =>
        Assert.Equal(expected, ProductKindClassifier.Classify(title).Code);

    [Theory]
    [InlineData("Pokémon TCG: Mega Evolution 5 - Pitch Black Premium Checklane Blister - Luxray", "luxray premium")]
    [InlineData("Pitch Black Premium Checklane Blister Luxray", "luxray premium")]
    [InlineData("Pokémon TCG Pitch Black Checklane Blister Ingles", "")]
    [InlineData("Pokémon TCG Pitch Black Premium Checklane Ingles", "premium")]
    [InlineData("Pokémon TCG: Mega Evolution 5 - Pitch Black Checklane Blister - Slowpoke", "slowpoke")]
    [InlineData("Pokémon TCG: Mega Evolution 5 - Pitch Black 3-pack blister - Binacle", "binacle")]
    public void Variant_KeepsOnlyTheDistinctiveWords(string title, string expected)
    {
        var kind = ProductKindClassifier.Classify(title);

        Assert.Equal(expected, ProductKindClassifier.Variant(title, kind, ["pitch black"]));
    }

    [Theory]
    [InlineData("PKM - Mega Evolution 5 - Pitch Black Elite Trainer Box")]
    [InlineData("Pokémon TCG: Mega Evolution 5 - Pitch Black Elite Trainer Box")]
    [InlineData("Mega Evolution 5 - Pitch Black Elite Trainer Box - EN")]
    [InlineData("Pitch Black Booster Bundle – MAX 1")]
    [InlineData("Pokémon TCG: Mega Evolution 5 - Pitch Black Booster Display (36 Booster)")]
    public void Variant_IsEmpty_WhenTheTitleIsOnlySetKindAndNoise(string title) =>
        Assert.Equal(string.Empty, ProductKindClassifier.Variant(title, ProductKindClassifier.Classify(title), ["pitch black"]));

    [Theory]
    [InlineData("Pokémon TCG: Mega Evolution - Perfect Order - Elite Trainer Box", "Perfect Order")]
    [InlineData("Stellar Crown Elite Trainer Box [Pré-venda]", "Stellar Crown")]
    [InlineData("PRE-ORDER 30th Celebration Elite Trainer Box", "30th Celebration")]
    public void VariantLabel_KeepsTheTitlesOrderAndCasing(string title, string expected) =>
        Assert.Equal(expected, ProductKindClassifier.VariantLabel(title, ProductKindClassifier.Classify(title), ["elite trainer box"]));

    [Fact]
    public void Variant_IgnoresThePreOrderMark()
    {
        var kind = ProductKindClassifier.Classify("Pitch Black Elite Trainer Box");

        Assert.Equal(
            ProductKindClassifier.Variant("Pitch Black Elite Trainer Box", kind, ["elite trainer box"]),
            ProductKindClassifier.Variant("Pitch Black Elite Trainer Box [Pré-venda]", kind, ["elite trainer box"]));
    }

    [Fact]
    public void Variant_KeepsTheSet_ForATaskThatIsNotAboutOneSet()
    {
        var title = "Stellar Crown Elite Trainer Box";

        Assert.Equal("crown stellar", ProductKindClassifier.Variant(title, ProductKindClassifier.Classify(title), ["elite trainer box"]));
    }
}
