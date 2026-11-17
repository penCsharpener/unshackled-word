using UnshackledWord.Application.Features.VerseMapping;

namespace UnshackledWord.Application.Tests.Features;

public class GermanWordPartsTests
{
    [Theory]
    [InlineData("Gerstenernte", "Gersten", true)]
    [InlineData("Gerstenernte", "ernte", true)]
    [InlineData("Gerstenernte", "GERSTEN", true)]
    [InlineData("Beth-hakkerem", "Beth-", true)]
    [InlineData("Kirjath-Jearim", "Jearim", true)]
    [InlineData("Gerstenernte", "Gerstenernte", false)]       // the whole word
    [InlineData("Gerstenernte", "gerstenernte", false)]
    [InlineData("Migdal-Heder", "MigdalHeder", false)]        // the whole word without its hyphen
    [InlineData("Gerstenernte", "und", false)]                // not in the word
    [InlineData("Gerstenernte", "1", false)]
    [InlineData("Gerstenernte", "null", false)]
    [InlineData("Gerstenernte", "*", false)]
    [InlineData("Beth-hakkerem", "-", false)]                 // a substring, but no letter
    [InlineData("Beth-hakkerem", "--", false)]
    [InlineData("Gerstenernte", "", false)]
    [InlineData("Gerstenernte", null, false)]
    [InlineData("", "Gersten", false)]
    [InlineData(null, "Gersten", false)]
    public void IsRealPart_FollowsTheCleanupRule(string? word, string? part, bool expected)
    {
        GermanWordParts.IsRealPart(word, part).ShouldBe(expected);
    }

    [Fact]
    public void CleanPart_ReturnsThePartOnlyWhenItIsReal()
    {
        GermanWordParts.CleanPart("Gerstenernte", "Gersten").ShouldBe("Gersten");
        GermanWordParts.CleanPart("Gerstenernte", "Gerstenernte").ShouldBeNull();
    }

    [Theory]
    [InlineData("Gerstenernte.", "gersten-ERNTE", true)]
    [InlineData("Gerstenernte", "Gerstenernt", false)]
    public void Normalize_DropsHyphensPunctuationAndCase(string word, string parts, bool equal)
    {
        (GermanWordParts.Normalize(word) == GermanWordParts.Normalize(parts)).ShouldBe(equal);
    }

    [Fact]
    public void DeriveOrders_FollowsThePositionInTheWord_AndSharesTheOrderOfEqualParts()
    {
        GermanWordParts.DeriveOrders("Gerstenernte.", ["ernte", "Gersten", "ernte"]).ShouldBe([2, 1, 2]);
        GermanWordParts.DeriveOrders("Gerstentenne", ["tenne", "Gersten"]).ShouldBe([2, 1]);
    }
}
