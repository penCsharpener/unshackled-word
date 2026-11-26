using UnshackledWord.Application.Features.GrammarSearch;
using UnshackledWord.Domain.Models.Grammar;

namespace UnshackledWord.Application.Tests.Features.GrammarSearch;

public class GrammarFacetFolderTests
{
    private static MorphologyCodeEntry Entry(string code) => new(code, GreekMorphologyDecoder.Decode(code));

    private static GrammarKeyDef Key(string name) => GrammarVocabulary.FindKey(name, false)!;

    [Fact]
    public void Fold_SumsCodesPerValue_InVocabularyOrder_WithoutZeroValues()
    {
        var codes = new[] { Entry("N-NSF"), Entry("N-NPF"), Entry("N-GSF"), Entry("N-ASM") };
        var counts = new Dictionary<string, int> { ["N-NSF"] = 20, ["N-NPF"] = 16, ["N-GSF"] = 18, ["N-ASM"] = 33, ["V-PAI-3S"] = 99 };

        var facet = GrammarFacetFolder.Fold(Key("Kasus"), counts, codes);

        facet.Key.ShouldBe("Kasus");
        facet.Values.Select(v => (v.LabelDe, v.Count)).ShouldBe([("Nominativ", 36), ("Genitiv", 18), ("Akkusativ", 33)]);
    }

    [Fact]
    public void Fold_IgnoresCodesWithoutTheKey_AndCodesWithoutCount()
    {
        var codes = new[] { Entry("V-PAI-3S"), Entry("N-NSF") };
        var counts = new Dictionary<string, int> { ["V-PAI-3S"] = 5, ["N-NSF"] = 0 };

        GrammarFacetFolder.Fold(Key("Kasus"), counts, codes).Values.ShouldBeEmpty();
        GrammarFacetFolder.Fold(Key("Wortart"), counts, codes).Values.Select(v => v.Count).ShouldBe([5]);
    }

    [Fact]
    public void Parse_EmptyValueIsAnErrorUnlessAllowed()
    {
        GrammarQueryParser.Parse("agape Kasus:").Error!.Code.ShouldBe(GrammarErrorCodes.UnknownValue);

        var parsed = GrammarQueryParser.Parse("agape Kasus:", allowEmptyValue: true);
        parsed.Ok.ShouldBeTrue();
        parsed.Slots.Single().Filters.ShouldBeEmpty();
        parsed.Slots.Single().Text.ShouldBe("agape Kasus:");

        GrammarQueryParser.Parse("Kasus:", allowEmptyValue: true).Slots.Single().Word.ShouldBeNull();
    }
}
