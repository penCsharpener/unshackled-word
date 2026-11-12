using UnshackledWord.Domain.Models.DataQuality;

namespace UnshackledWord.Domain.Tests.Model.DataQuality;

public class UnmappedRulesTests
{
    [Theory]
    [InlineData("NOUN", "added", "error")]
    [InlineData("VERB", "added", "error")]
    [InlineData("DET", "added", "info")]
    [InlineData("AUX", "added", "info")]
    [InlineData("DET", "none", "error")]
    [InlineData(null, "added", "info")]
    public void German_severity(string? pos, string kind, string expected) =>
        UnmappedRules.GermanSeverity(pos, kind).ShouldBe(expected);

    [Theory]
    [InlineData("G", "N", "G0001", null, true, true, "N")]
    [InlineData("G", "N", "G0001", null, false, false, "notNaTr")]
    [InlineData("G", "N", "G0001", null, true, false, "N")]
    [InlineData("G", "T", "G3588", null, true, true, "T")]
    [InlineData("H", "T", "H0853", "L", null, null, "HTo")]
    [InlineData("H", "N", "H0430", "Q(K)", null, null, "ketiv")]
    [InlineData("H", "N", "H0430", "L", null, null, "N")]
    [InlineData("H", null, null, "L", null, null, "?")]
    public void Source_class_key(string lang, string? cls, string? strongs, string? hebType, bool? inNa, bool? inTr, string expected) =>
        UnmappedRules.SourceClassKey(lang, cls, strongs, hebType, inNa, inTr).ShouldBe(expected);

    [Theory]
    [InlineData("G", "notNaTr", true, "info")]
    [InlineData("G", "T", true, "info")]
    [InlineData("H", "HTo", true, "info")]
    [InlineData("H", "ketiv", true, "info")]
    [InlineData("G", "N", false, "error")]
    [InlineData("G", "V", false, "error")]
    [InlineData("G", "ADV", false, "error")]
    [InlineData("G", "CONJ", false, "warning")]
    [InlineData("H", "N", false, "error")]
    [InlineData("H", "T", false, "warning")]
    [InlineData("H", "R", false, "warning")]
    public void Source_class_description(string lang, string key, bool legit, string severity)
    {
        var d = UnmappedRules.DescribeSource(lang, key);
        d.Legit.ShouldBe(legit);
        d.Severity.ShouldBe(severity);
    }

    [Fact]
    public void Error_keys_are_exactly_the_non_legit_error_classes()
    {
        foreach (var lang in new[] { "G", "H" })
        {
            foreach (var key in UnmappedRules.ErrorKeys(lang))
            {
                UnmappedRules.DescribeSource(lang, key).Severity.ShouldBe("error");
            }
        }
    }
}
