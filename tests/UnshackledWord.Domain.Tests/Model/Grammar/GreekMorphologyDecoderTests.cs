using UnshackledWord.Domain.Models.Grammar;

namespace UnshackledWord.Domain.Tests.Model.Grammar;

public class GreekMorphologyDecoderTests
{
    [Theory]
    [InlineData("N-NSM", "noun", null, null, null, null, "nominative", "singular", "masculine", null)]
    [InlineData("N-GPF", "noun", null, null, null, null, "genitive", "plural", "feminine", null)]
    [InlineData("V-IAI-3S", "verb", "imperfect", "active", "indicative", "third", null, "singular", null, null)]
    [InlineData("V-AAP-NSM", "verb", "aorist", "active", "participle", null, "nominative", "singular", "masculine", null)]
    [InlineData("V-PAN", "verb", "present", "active", "infinitive", null, null, null, null, null)]
    [InlineData("V-2AAI-3S", "verb", "aorist", "active", "indicative", "third", null, "singular", null, null)]
    [InlineData("T-NSM", "article", null, null, null, null, "nominative", "singular", "masculine", null)]
    [InlineData("P-1NS", "pronoun", null, null, null, "first", "nominative", "singular", null, null)]
    [InlineData("R-ASN", "pronoun", null, null, null, null, "accusative", "singular", "neuter", null)]
    [InlineData("A-NSM-C", "adjective", null, null, null, null, "nominative", "singular", "masculine", "comparative")]
    [InlineData("A-NSM-S", "adjective", null, null, null, null, "nominative", "singular", "masculine", "superlative")]
    [InlineData("V-RPP-GPM", "verb", "perfect", "passive", "participle", null, "genitive", "plural", "masculine", null)]
    [InlineData("V-FDI-1P", "verb", "future", "middle", "indicative", "first", null, "plural", null, null)]
    [InlineData("ADV", "adverb", null, null, null, null, null, null, null, null)]
    [InlineData("CONJ", "conjunction", null, null, null, null, null, null, null, null)]
    [InlineData("PREP", "preposition", null, null, null, null, null, null, null, null)]
    [InlineData("PRT-N", "particle", null, null, null, null, null, null, null, null)]
    [InlineData("N-PRI", "nounProper", null, null, null, null, null, null, null, null)]
    [InlineData("N-LI", "indeclinable", null, null, null, null, null, null, null, null)]
    [InlineData("A-NUI", "indeclinable", null, null, null, null, null, null, null, null)]
    public void Decode_KnownCode_ReturnsKeys(string code, string? pos, string? tense, string? voice, string? mood, string? person,
        string? @case, string? number, string? gender, string? degree)
    {
        var keys = GreekMorphologyDecoder.Decode(code);

        keys.PartOfSpeech.ShouldBe(pos);
        keys.Tense.ShouldBe(tense);
        keys.Voice.ShouldBe(voice);
        keys.Mood.ShouldBe(mood);
        keys.Person.ShouldBe(person);
        keys.Case.ShouldBe(@case);
        keys.Number.ShouldBe(number);
        keys.Gender.ShouldBe(gender);
        keys.Degree.ShouldBe(degree);
    }

    [Theory]
    [InlineData("XYZ")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("Z-NSM")]
    public void Decode_UnknownCode_ReturnsEmpty(string? code)
    {
        GreekMorphologyDecoder.Decode(code).IsEmpty.ShouldBeTrue();
    }
}
