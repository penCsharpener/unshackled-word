using UnshackledWord.Application.Features.Seed.Morphology;

namespace UnshackledWord.Application.Tests.Features.Seed.Morphology;

public class MorphologyFeaturesTests
{
    [Fact]
    public void Format_ShouldWriteKeysAlphabetically_WhenSeveralFeaturesAreSet()
    {
        var values = new MorphologyFeatureValues(Degree: null, VerbForm: null, Tense: null, Person: null, Number: "Sing", Mood: null, Case: "Nom", Gender: "Masc");

        MorphologyFeatures.Format(values).ShouldBe("Case=Nom|Gender=Masc|Number=Sing");
    }

    [Fact]
    public void Format_ShouldReturnEmpty_WhenNoFeatureIsSet()
    {
        MorphologyFeatures.Format(new MorphologyFeatureValues(null, null, null, null, null, null, null, null)).ShouldBeEmpty();
    }

    [Fact]
    public void Format_ShouldSkipEmptyStrings_WhenAColumnHoldsAnEmptyValue()
    {
        MorphologyFeatures.Format(new MorphologyFeatureValues(null, "", null, null, "Plur", null, null, null)).ShouldBe("Number=Plur");
    }

    [Fact]
    public void Format_ShouldUseTheFixedKeyOrder_WhenAllEightFeaturesAreSet()
    {
        var all = new MorphologyFeatureValues("Pos", "Fin", "Past", "3", "Sing", "Ind", "Nom", "Fem");

        MorphologyFeatures.Format(all).ShouldBe("Case=Nom|Degree=Pos|Gender=Fem|Mood=Ind|Number=Sing|Person=3|Tense=Past|VerbForm=Fin");
        MorphologyFeatures.Format(all).Split('|').Select(p => p.Split('=')[0]).ShouldBe(MorphologyFeatures.KeyOrder);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Case=Nom|Gender=Masc|Number=Sing")]
    [InlineData("Degree=Pos|Mood=Ind|Person=3|Tense=Past|VerbForm=Fin")]
    public void Parse_ShouldInvertFormat_WhenGivenAFormattedString(string feats)
    {
        MorphologyFeatures.Format(MorphologyFeatures.Parse(feats)).ShouldBe(feats);
    }

    [Fact]
    public void Parse_ShouldIgnoreUnknownKeysAndGarbage_WhenSeedIsNewer()
    {
        var values = MorphologyFeatures.Parse("Animacy=Anim|Case=Dat|nonsense|=x");

        values.Case.ShouldBe("Dat");
        values.Gender.ShouldBeNull();
    }
}
