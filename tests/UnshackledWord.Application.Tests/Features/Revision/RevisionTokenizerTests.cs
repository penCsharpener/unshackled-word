using UnshackledWord.Application.Features.Revision;

namespace UnshackledWord.Application.Tests.Features.Revision;

public class RevisionTokenizerTests
{
    [Fact]
    public void Tokenize_CollapsesWhitespaceTrimsAndNumbersFromOne()
    {
        var tokens = RevisionTokenizer.Tokenize("  Im \t Anfang\n schuf   Gott  ");

        tokens.Select(t => t.Position).ShouldBe([1, 2, 3, 4]);
        tokens.Select(t => t.WordInContext).ShouldBe(["Im", "Anfang", "schuf", "Gott"]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \t ")]
    public void Tokenize_EmptyInput_ReturnsNoTokens(string? text) => RevisionTokenizer.Tokenize(text).ShouldBeEmpty();

    [Fact]
    public void Tokenize_PunctuationStaysInWordInContextButNotInPlainWord()
    {
        var tokens = RevisionTokenizer.Tokenize("Gott, der: Herr; (wer?) „so“.");

        tokens[0].ShouldBe(new RevisionToken(1, "Gott,", "Gott"));
        tokens[1].PlainWord.ShouldBe("der");
        tokens[2].PlainWord.ShouldBe("Herr");
        tokens[3].PlainWord.ShouldBe("wer");
    }

    [Fact]
    public void Tokenize_TrimsDashesAtTheEndsOnly()
    {
        var tokens = RevisionTokenizer.Tokenize("-und Gott-Vater --");

        tokens[0].PlainWord.ShouldBe("und");
        tokens[1].PlainWord.ShouldBe("Gott-Vater");
        tokens[2].PlainWord.ShouldBe("");
    }

    [Fact]
    public void Tokenize_RemovesApostrophesAndBrackets()
    {
        var tokens = RevisionTokenizer.Tokenize("{Gottes} [Sohn] ’s 'x'");

        tokens.Select(t => t.PlainWord).ShouldBe(["Gottes", "Sohn", "s", "x"]);
    }

    [Fact]
    public void Tokenize_PunctuationOnlyToken_HasEmptyPlainWord()
    {
        var tokens = RevisionTokenizer.Tokenize("Wort ¶ . Ende");

        tokens[1].ShouldBe(new RevisionToken(2, "¶", ""));
        tokens[2].PlainWord.ShouldBe("");
        tokens[3].Position.ShouldBe(4);
    }
}

