using UnshackledWord.Domain.Models.Grammar;

namespace UnshackledWord.Domain.Tests.Model.Grammar;

public class OriginalLanguageRulesTests
{
    private static OriginalCell Cell(string key, string? gloss, string origPos, string germanPos, long count, string kind = "aligned") =>
        new(key, kind, "w", gloss, origPos, germanPos, count);

    [Theory]
    [InlineData("G4680", true)]
    [InlineData("H0430", true)]
    [InlineData("G2316+G3588", true)]
    [InlineData("added", true)]
    [InlineData("none", true)]
    [InlineData("", false)]
    [InlineData("G12", false)]
    [InlineData("X1234", false)]
    [InlineData("Added", false)]
    public void Original_key_validation(string key, bool ok) => OriginalLanguageRules.IsValidOriginalKey(key).ShouldBe(ok);

    [Fact]
    public void Expected_original_accepts_empty_and_matches_added_or_none()
    {
        OriginalLanguageRules.IsValidExpectedOriginal("").ShouldBeTrue();
        OriginalLanguageRules.KeyMatchesExpected("", "added").ShouldBeTrue();
        OriginalLanguageRules.KeyMatchesExpected("", "none").ShouldBeTrue();
        OriginalLanguageRules.KeyMatchesExpected("", "G1234").ShouldBeFalse();
        OriginalLanguageRules.KeyMatchesExpected("G1234", "G1234").ShouldBeTrue();
        OriginalLanguageRules.KeyMatchesExpected("G1234", "G1235").ShouldBeFalse();
    }

    [Theory]
    [InlineData("Lord", "lord", false)]
    [InlineData("wise", "be wise", false)]
    [InlineData("wise", "way", true)]
    [InlineData("gate", "fool", true)]
    [InlineData("God", "God (Aramaic)", false)]
    [InlineData(null, "way", false)]
    public void Gloss_difference_ignores_synonyms(string? a, string? b, bool differ) =>
        OriginalLanguageRules.GlossesDiffer(a, b).ShouldBe(differ);

    [Fact]
    public void Row_pos_conflict_flags_sein_aux_to_possessive_but_not_hebrew_function_words()
    {
        OriginalLanguageRules.RowPosConflict("AUX", "P", "G0846", "aligned").ShouldBeTrue();
        OriginalLanguageRules.RowPosConflict("AUX", "V", "G1510", "aligned").ShouldBeFalse();
        OriginalLanguageRules.RowPosConflict("DET", "N", "H1121", "aligned").ShouldBeFalse(); // OT function word: host word
        OriginalLanguageRules.RowPosConflict("NOUN", "V", "H1234", "aligned").ShouldBeTrue();
        OriginalLanguageRules.RowPosConflict("NOUN", "V", "added", "added").ShouldBeFalse();
        OriginalLanguageRules.RowPosConflict("ADV", "V", "G1234", "aligned").ShouldBeFalse(); // no expectation
    }

    [Fact]
    public void Homograph_weise_is_ambiguous_and_marks_small_groups_as_minority()
    {
        var summary = OriginalLanguageRules.Summarize(
        [
            Cell("H2450", "wise", "A", "NOUN", 32),
            Cell("H2449", "be wise", "V", "NOUN", 0 + 19),
            Cell("H4941", "judgment", "N", "NOUN", 14),
            Cell("G4680", "wise", "A", "NOUN", 9),
            Cell("G5158", "way", "N", "NOUN", 4),
        ]);

        summary.Ambiguous.ShouldBeTrue(); // H2450 (wise) vs H4941 (judgment), both >= 10 %
        summary.Groups.Single(g => g.Key == "G5158").Minority.ShouldBeFalse(); // 4 of 13 Greek = 31 %
        summary.Groups.Single(g => g.Key == "H2449").PosConflict.ShouldBeTrue(); // NOUN -> V
    }

    [Fact]
    public void Synonym_set_herr_does_not_warn()
    {
        var summary = OriginalLanguageRules.Summarize(
        [
            Cell("G2962", "lord", "N", "NOUN", 722),
            Cell("H0136", "Lord", "N", "NOUN", 433),
            Cell("H0113", "lord", "N", "NOUN", 329),
            Cell("H1167", "lord", "N", "NOUN", 7),
        ]);

        summary.Ambiguous.ShouldBeFalse();
    }

    [Fact]
    public void Rare_group_is_minority_and_not_top()
    {
        var summary = OriginalLanguageRules.Summarize(
        [
            Cell("G2424", "Jesus", "N", "PROPN", 928),
            Cell("G3588", "the", "T", "PROPN", 5),
            Cell("added", null, "", "PROPN", 1, "added"),
        ]);

        var rare = summary.Groups.Single(g => g.Key == "G3588");
        rare.Minority.ShouldBeTrue();
        rare.PosConflict.ShouldBeTrue();
        summary.Groups.Single(g => g.Key == "G2424").Minority.ShouldBeFalse();
        summary.Groups.Single(g => g.Key == "added").Added.ShouldBeTrue();
    }

    [Fact]
    public void Hebrew_function_word_groups_are_not_flagged()
    {
        var summary = OriginalLanguageRules.Summarize(
        [
            Cell("H0853", "[object]", "T", "DET", 3273),
            Cell("H1121", "son", "N", "DET", 2920),
            Cell("H0776", "land", "N", "DET", 2),
        ]);

        summary.Ambiguous.ShouldBeFalse();
        summary.Groups.ShouldAllBe(g => !g.Minority && !g.PosConflict);
    }

    [Fact]
    public void Sein_aux_mapped_to_possessive_sets_pos_conflict_and_ambiguity()
    {
        var summary = OriginalLanguageRules.Summarize(
        [
            Cell("G1510", "be", "V", "AUX", 3112),
            Cell("G0846", "he", "P", "AUX", 209),
        ]);

        summary.Groups.Single(g => g.Key == "G0846").PosConflict.ShouldBeTrue();
        summary.Ambiguous.ShouldBeTrue();
    }
}
