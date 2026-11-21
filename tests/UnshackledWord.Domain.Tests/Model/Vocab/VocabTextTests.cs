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
}
