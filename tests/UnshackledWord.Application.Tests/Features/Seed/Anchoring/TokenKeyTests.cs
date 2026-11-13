using System.Text;
using UnshackledWord.Application.Features.Seed.Anchoring;

namespace UnshackledWord.Application.Tests.Features.Seed.Anchoring;

public class TokenKeyTests
{
    [Theory]
    [InlineData("Anfang,", "Anfang")]
    [InlineData("„Gott“", "„Gott“")]
    [InlineData("(und)", "und")]
    [InlineData("Licht!", "Licht")]
    [InlineData("-Wort-", "Wort")]
    [InlineData("  Erde. ", "Erde")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Normalize_ShouldStripPunctuationLikePlainWord_WhenGivenRawWord(string? raw, string expected)
    {
        TokenKey.Normalize(raw).ShouldBe(expected);
    }

    [Fact]
    public void Normalize_ShouldKeepCase_WhenWordIsCapitalised()
    {
        TokenKey.Normalize("Gott").ShouldNotBe(TokenKey.Normalize("gott"));
    }

    [Fact]
    public void Normalize_ShouldComposeToNfc_WhenGivenDecomposedUmlaut()
    {
        var decomposed = "über";

        TokenKey.Normalize(decomposed).ShouldBe("über");
        TokenKey.Normalize(decomposed).IsNormalized(NormalizationForm.FormC).ShouldBeTrue();
    }
}
