using UnshackledWord.Domain.Models.Vocab;

namespace UnshackledWord.Domain.Tests.Model.Vocab;

public class VocabTextTests
{
    [Theory]
    [InlineData("ἀρχή", "αρχη")]
    [InlineData("Ἰησοῦς", "ιησουσ")]
    [InlineData("בְּרֵאשִׁית", "בראשית")]
    [InlineData("  λόγος ", "λογοσ")]
    [InlineData("ὁ", "ο")]
    [InlineData("Σαούλ", "σαουλ")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    public void Normalize_FoldsAccentsMarksAndFinalSigma(string input, string expected)
    {
        VocabText.Normalize(input).ShouldBe(expected);
    }

    [Fact]
    public void Normalize_TreatsPrecomposedAndDecomposedInputTheSame()
    {
        VocabText.Normalize("ἀρχή".Normalize(System.Text.NormalizationForm.FormC))
            .ShouldBe(VocabText.Normalize("ἀρχή".Normalize(System.Text.NormalizationForm.FormD)));
    }

    [Fact]
    public void Normalize_Null_IsEmpty()
    {
        VocabText.Normalize(null).ShouldBe(string.Empty);
    }

    [Theory]
    [InlineData("μέσος, -η, -ον", "μέσος|, -|η|, -|ον", "5,1,2")]
    [InlineData("λόγος, -ου, ὁ", "λόγος|, -|ου|, |ὁ", "5,2,1")]
    [InlineData("ἀρχή", "ἀρχή", "4")]
    [InlineData("בְּרֵאשִׁית", "בְּרֵאשִׁית", "6")]
    public void Segment_SplitsLetterGroupsAndSeparators(string input, string expectedTexts, string expectedLengths)
    {
        var segments = VocabText.Segment(input);

        string.Join("|", segments.Select(x => x.Text)).ShouldBe(expectedTexts.Normalize(System.Text.NormalizationForm.FormC));
        string.Join(",", segments.Where(x => x.IsGroup).Select(x => x.Length)).ShouldBe(expectedLengths);
    }

    [Fact]
    public void Segment_AlternatesGroupsAndSeparators()
    {
        VocabText.Segment("μέσος, -η, -ον").Select(x => x.IsGroup).ShouldBe([true, false, true, false, true]);
        VocabText.Segment("ἀρχή").Count.ShouldBe(1);
        VocabText.Segment("").ShouldBeEmpty();
    }

    [Theory]
    [InlineData("ἀρχή", "αρχη", true)]
    [InlineData("Ἰησοῦς", "ιησους", true)]
    [InlineData("בְּרֵאשִׁית", "בראשית", true)]
    [InlineData("η", "ος", false)]
    public void Normalize_ComparesTypedAnswers(string expected, string given, bool equal)
    {
        (VocabText.Normalize(given) == VocabText.Normalize(expected)).ShouldBe(equal);
    }
}
