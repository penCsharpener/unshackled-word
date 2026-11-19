using UnshackledWord.Domain.Models.DataQuality;

namespace UnshackledWord.Domain.Tests.Model.DataQuality;

public class TranslitFoldingTests
{
    [Theory]
    [InlineData("Ἰησοῦς", "ιησουσ")]
    [InlineData("Iēsous", "iesous")]
    [InlineData("λόγος", "λογοσ")]
    [InlineData("e.lo.him", "elohim")]
    [InlineData("'Ish", "ish")]
    [InlineData("דָּבָר", "דבר")]
    [InlineData("  ", "")]
    public void Fold_drops_marks_separators_and_final_sigma(string input, string expected) =>
        TranslitFolding.Fold(input).ShouldBe(expected);

    [Theory]
    [InlineData("מֶלֶךְ", "מלכ")]
    [InlineData("מלך", "מלכ")]
    [InlineData("אֶרֶץ", "ארצ")]
    public void Fold_normalises_the_hebrew_final_forms(string input, string expected) =>
        TranslitFolding.Fold(input).ShouldBe(expected);

    [Theory]
    [InlineData("ἀγάπη,", "αγαπη")]
    [InlineData("τί;", "τι")]
    [InlineData("ἀγάπη·", "αγαπη")]
    [InlineData("אֶת־הָאָרֶץ", "אתהארצ")]
    [InlineData("λόγος θεός", "λογοσ θεοσ")]
    public void FoldScript_also_drops_punctuation_but_keeps_spaces(string input, string expected) =>
        TranslitFolding.FoldScript(input).ShouldBe(expected);

    [Theory]
    [InlineData("Phaneros", "faneros")]
    [InlineData("wayyiqra", "vayyiqra")]
    [InlineData("khesed", "chesed")]
    [InlineData("xesed", "chesed")]
    [InlineData("tzedek", "tsedek")]
    [InlineData("ṣedeq", "tsedeq")]
    public void Latin_applies_the_equivalences(string a, string b) =>
        TranslitFolding.Latin(a).ShouldBe(TranslitFolding.Latin(b));

    [Theory]
    [InlineData("dbr", "dabar")]
    [InlineData("dabar", "da.var")]
    [InlineData("dbr", "da.var")]
    [InlineData("kthb", "ketav")]
    public void Skeleton_ignores_vowels_and_spirantisation(string a, string b) =>
        TranslitFolding.Skeleton(a).ShouldBe(TranslitFolding.Skeleton(b));

    [Fact]
    public void Skeleton_keeps_different_consonants_apart() =>
        TranslitFolding.Skeleton("dabar").ShouldNotBe(TranslitFolding.Skeleton("delet"));

    [Theory]
    [InlineData("G1577", "G1577")]
    [InlineData("g1577", "G1577")]
    [InlineData("H430", "H0430")]
    [InlineData("h0430", "H0430")]
    [InlineData("G2424a", "G2424")]
    public void Strongs_with_language_normalizes_to_the_padded_base_key(string input, string expected) =>
        TranslitFolding.NormalizeStrongsKey(input).ShouldBe(expected);

    [Fact]
    public void Bare_number_yields_both_languages() =>
        TranslitFolding.ParseStrongs("2424").ShouldBe(["G2424", "H2424"]);

    [Theory]
    [InlineData("")]
    [InlineData("Jesus")]
    [InlineData("G")]
    [InlineData("G0")]
    [InlineData("G12345")]
    [InlineData("X1234")]
    public void Non_strongs_input_is_rejected(string input) =>
        TranslitFolding.ParseStrongs(input).ShouldBeEmpty();
}
