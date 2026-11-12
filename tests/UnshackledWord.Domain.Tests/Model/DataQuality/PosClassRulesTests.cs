using UnshackledWord.Domain.Models.DataQuality;

namespace UnshackledWord.Domain.Tests.Model.DataQuality;

public class PosClassRulesTests
{
    [Theory]
    [InlineData("G", "NOUN", "N", true)]
    [InlineData("G", "PROPN", "N", true)]
    [InlineData("G", "VERB", "V", true)]
    [InlineData("G", "DET", "T", true)]
    [InlineData("G", "AUX", "P", false)]
    [InlineData("G", "NOUN", "T", false)]
    [InlineData("H", "VERB", "V", true)]
    [InlineData("H", "NOUN", "R", false)]
    [InlineData("G", "X", "T", true)]
    [InlineData("G", null, "T", true)]
    [InlineData("G", "NOUN", null, true)]
    public void IsExpected_follows_the_table(string lang, string? pos, string? cls, bool expected) =>
        PosClassRules.IsExpected(lang, pos, cls).ShouldBe(expected);

    [Theory]
    [InlineData("DET")]
    [InlineData("ADP")]
    [InlineData("CCONJ")]
    [InlineData("SCONJ")]
    [InlineData("PRON")]
    [InlineData("AUX")]
    [InlineData("PART")]
    public void Hebrew_function_words_are_never_compared(string pos)
    {
        PosClassRules.IsExpected("H", pos, "N").ShouldBeTrue();
        PosClassRules.IsContentFunctionMismatch("H", pos, "N").ShouldBeFalse();
        PosClassRules.IsExpected("G", pos, "ZZ").ShouldBeFalse();
    }

    [Theory]
    [InlineData("G", "NOUN", "T", true)]
    [InlineData("G", "VERB", "CONJ", true)]
    [InlineData("H", "NOUN", "C", true)]
    [InlineData("G", "DET", "N", true)]
    [InlineData("G", "ADP", "V", true)]
    [InlineData("H", "DET", "N", false)]
    [InlineData("G", "NOUN", "N", false)]
    [InlineData("G", "DET", "T", false)]
    public void Content_function_mismatch(string lang, string pos, string cls, bool expected) =>
        PosClassRules.IsContentFunctionMismatch(lang, pos, cls).ShouldBe(expected);

    [Fact]
    public void Sql_key_lists_agree_with_the_predicates()
    {
        var expected = PosClassRules.ExpectedKeys().ToHashSet();
        expected.ShouldContain("G|NOUN|N");
        expected.ShouldNotContain("G|NOUN|T");
        foreach (var key in PosClassRules.ContentFunctionKeys())
        {
            var parts = key.Split('|');
            PosClassRules.IsContentFunctionMismatch(parts[0], parts[1], parts[2]).ShouldBeTrue(key);
        }
    }

    [Theory]
    [InlineData("PROPN", false, true)]
    [InlineData("PROPN", true, false)]
    [InlineData("NOUN", true, true)]
    [InlineData("NOUN", false, false)]
    [InlineData("VERB", true, false)]
    public void Proper_noun_conflict(string pos, bool properNoun, bool conflict) =>
        PosClassRules.IsProperNounConflict(pos, properNoun).ShouldBe(conflict);
}
