using UnshackledWord.Domain.Models.Vocab;

namespace UnshackledWord.Domain.Tests.Model.Vocab;

public class VocabChangeRulesTests
{
    [Fact]
    public void AcceptedSetChanged_IgnoresOrder() =>
        VocabChangeRules.AcceptedSetChanged(["Wort", "Rede"], ["Rede", "Wort"]).ShouldBeFalse();

    [Fact]
    public void AcceptedSetChanged_IsCaseSensitive() =>
        VocabChangeRules.AcceptedSetChanged(["Wort"], ["wort"]).ShouldBeTrue();

    [Fact]
    public void AcceptedSetChanged_DetectsAddedAndRemoved()
    {
        VocabChangeRules.AcceptedSetChanged(["Wort"], ["Wort", "Rede"]).ShouldBeTrue();
        VocabChangeRules.AcceptedSetChanged(["Wort", "Rede"], ["Wort"]).ShouldBeTrue();
    }

    [Fact]
    public void PlanForm_UnchangedKeepsOrigin()
    {
        var plan = VocabChangeRules.PlanForm("λόγος, -ου, ὁ", "generated", "  λόγος, -ου, ὁ ");

        plan.Changed.ShouldBeFalse();
        plan.Origin.ShouldBe("generated");
    }

    [Fact]
    public void PlanForm_NewTextIsManualAndBumps()
    {
        var plan = VocabChangeRules.PlanForm(null, null, "μέσος, -η, -ον");

        plan.Changed.ShouldBeTrue();
        plan.DictionaryForm.ShouldBe("μέσος, -η, -ον");
        plan.Origin.ShouldBe("manual");
    }

    [Fact]
    public void PlanForm_BlankClearsFormAndOrigin()
    {
        var plan = VocabChangeRules.PlanForm("x", "agent", "   ");

        plan.Changed.ShouldBeTrue();
        plan.DictionaryForm.ShouldBeNull();
        plan.Origin.ShouldBeNull();
        VocabChangeRules.PlanForm(null, null, "").Changed.ShouldBeFalse();
    }

    [Fact]
    public void PlanMeaningOrder_NumbersFromOne()
    {
        var ranks = VocabChangeRules.PlanMeaningOrder([4, 7, 9], [9, 4, 7]);

        ranks.ShouldNotBeNull();
        ranks![9].ShouldBe(1);
        ranks[4].ShouldBe(2);
        ranks[7].ShouldBe(3);
    }

    [Theory]
    [InlineData(new[] { 4, 7 })]
    [InlineData(new[] { 4, 7, 9, 9 })]
    [InlineData(new[] { 4, 7, 8 })]
    [InlineData(new[] { 4, 4, 7 })]
    public void PlanMeaningOrder_RejectsAnythingButTheCurrentIds(int[] requested) =>
        VocabChangeRules.PlanMeaningOrder([4, 7, 9], requested).ShouldBeNull();

    [Theory]
    [InlineData("G3056", 'G', 3056)]
    [InlineData("H1254B", 'H', 1254)]
    [InlineData("H9001", 'H', 9001)]
    public void TryParseKey_ParsesKeys(string key, char lang, int number)
    {
        VocabChangeRules.TryParseKey(key, out var l, out var n).ShouldBeTrue();
        l.ShouldBe(lang);
        n.ShouldBe(number);
    }

    [Theory]
    [InlineData("")]
    [InlineData("G")]
    [InlineData("X123")]
    [InlineData("G12AB")]
    [InlineData("G30A5")]
    [InlineData("g3056")]
    public void TryParseKey_RejectsNonKeys(string key) => VocabChangeRules.TryParseKey(key, out _, out _).ShouldBeFalse();

    [Theory]
    [InlineData("H9001", true)]
    [InlineData("H9049", true)]
    [InlineData("H9050", false)]
    [InlineData("G9001", false)]
    public void IsMorphemeKey_OnlyHebrewPrefixRange(string key, bool expected) => VocabChangeRules.IsMorphemeKey(key).ShouldBe(expected);
}
