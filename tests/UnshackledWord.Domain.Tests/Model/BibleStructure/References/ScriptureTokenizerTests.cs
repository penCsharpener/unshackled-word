using UnshackledWord.Domain.Models.BibleStructure.References;

namespace UnshackledWord.Domain.Tests.Model.BibleStructure.References;

public class ScriptureTokenizerTests
{
    private const ScriptureTokenKind Ws = ScriptureTokenKind.Whitespace;
    private const ScriptureTokenKind Num = ScriptureTokenKind.Number;
    private const ScriptureTokenKind Word = ScriptureTokenKind.Word;
    private const ScriptureTokenKind Colon = ScriptureTokenKind.Colon;
    private const ScriptureTokenKind Comma = ScriptureTokenKind.Comma;
    private const ScriptureTokenKind Dot = ScriptureTokenKind.Dot;
    private const ScriptureTokenKind Semi = ScriptureTokenKind.Semicolon;
    private const ScriptureTokenKind Dash = ScriptureTokenKind.Dash;
    private const ScriptureTokenKind Other = ScriptureTokenKind.Other;

    [Fact]
    public void Tokenize_OffbVerseListWithDashes_ExactKindsTextAndPositions()
    {
        var tokens = ScriptureTokenizer.Tokenize("Offb 2,1-5.7.9-10");

        tokens.ShouldBe(new[]
        {
            Tok(Word, "Offb", 0),
            Tok(Ws, " ", 4),
            Tok(Num, "2", 5),
            Tok(Comma, ",", 6),
            Tok(Num, "1", 7),
            Tok(Dash, "-", 8),
            Tok(Num, "5", 9),
            Tok(Dot, ".", 10),
            Tok(Num, "7", 11),
            Tok(Dot, ".", 12),
            Tok(Num, "9", 13),
            Tok(Dash, "-", 14),
            Tok(Num, "10", 15),
        });
    }

    [Fact]
    public void Tokenize_RomerWithChapterSemicolonAndUmlaut_ExactKindsTextAndPositions()
    {
        var tokens = ScriptureTokenizer.Tokenize("Röm 8,28; 12,1-2");

        tokens.ShouldBe(new[]
        {
            Tok(Word, "Röm", 0),
            Tok(Ws, " ", 3),
            Tok(Num, "8", 4),
            Tok(Comma, ",", 5),
            Tok(Num, "28", 6),
            Tok(Semi, ";", 8),
            Tok(Ws, " ", 9),
            Tok(Num, "12", 10),
            Tok(Comma, ",", 12),
            Tok(Num, "1", 13),
            Tok(Dash, "-", 14),
            Tok(Num, "2", 15),
        });
    }

    [Fact]
    public void Tokenize_OrdinalWithDot_NumberDotWhitespaceWord()
    {
        var tokens = ScriptureTokenizer.Tokenize("1. Kor");

        tokens.ShouldBe(new[]
        {
            Tok(Num, "1", 0),
            Tok(Dot, ".", 1),
            Tok(Ws, " ", 2),
            Tok(Word, "Kor", 3),
        });
    }

    [Fact]
    public void Tokenize_EnDash_IsDashToken()
    {
        var tokens = ScriptureTokenizer.Tokenize("Joh 3,16–18");

        tokens.ShouldBe(new[]
        {
            Tok(Word, "Joh", 0),
            Tok(Ws, " ", 3),
            Tok(Num, "3", 4),
            Tok(Comma, ",", 5),
            Tok(Num, "16", 6),
            Tok(Dash, "–", 8),
            Tok(Num, "18", 9),
        });
    }

    [Fact]
    public void Tokenize_OsisDotsAndCommaSpace_ExactKindsTextAndPositions()
    {
        var tokens = ScriptureTokenizer.Tokenize("Mat.25.33, 34");

        tokens.ShouldBe(new[]
        {
            Tok(Word, "Mat", 0),
            Tok(Dot, ".", 3),
            Tok(Num, "25", 4),
            Tok(Dot, ".", 6),
            Tok(Num, "33", 7),
            Tok(Comma, ",", 9),
            Tok(Ws, " ", 10),
            Tok(Num, "34", 11),
        });
    }

    [Fact]
    public void Tokenize_PrecomposedUmlaut_IsOneWordToken()
    {
        var tokens = ScriptureTokenizer.Tokenize("Röm");

        tokens.ShouldBe(new[] { Tok(Word, "Röm", 0) });
        tokens[0].Length.ShouldBe(3);
    }

    [Fact]
    public void Tokenize_DecomposedUmlaut_StaysOneWordToken()
    {
        // "o" followed by U+0308 (combining diaeresis), as produced by NFD input
        var tokens = ScriptureTokenizer.Tokenize("Röm 8");

        tokens.ShouldBe(new[]
        {
            Tok(Word, "Röm", 0),
            Tok(Ws, " ", 4),
            Tok(Num, "8", 5),
        });
    }

    [Theory]
    [InlineData("-")]
    [InlineData("‐")]
    [InlineData("‑")]
    [InlineData("‒")]
    [InlineData("–")]
    [InlineData("—")]
    [InlineData("―")]
    [InlineData("−")]
    public void Tokenize_AnyDashCharacter_IsDashToken(string dash)
    {
        var tokens = ScriptureTokenizer.Tokenize(dash);

        tokens.ShouldBe(new[] { Tok(Dash, dash, 0) });
    }

    [Fact]
    public void Tokenize_ColonAndWhitespaceRuns_GroupedPerRun()
    {
        var tokens = ScriptureTokenizer.Tokenize("Joh\t 3:16\n\n18");

        tokens.ShouldBe(new[]
        {
            Tok(Word, "Joh", 0),
            Tok(Ws, "\t ", 3),
            Tok(Num, "3", 5),
            Tok(Colon, ":", 6),
            Tok(Num, "16", 7),
            Tok(Ws, "\n\n", 9),
            Tok(Num, "18", 11),
        });
    }

    [Fact]
    public void Tokenize_UnknownCharacters_AreOtherTokens()
    {
        var tokens = ScriptureTokenizer.Tokenize("@(");

        tokens.ShouldBe(new[]
        {
            Tok(Other, "@", 0),
            Tok(Other, "(", 1),
        });
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Tokenize_NullOrEmpty_ReturnsNoTokens(string? input)
    {
        ScriptureTokenizer.Tokenize(input).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("Offb 2,1-5.7.9-10")]
    [InlineData("Röm 8,28; 12,1-2")]
    [InlineData("1. Kor 13,4–7")]
    [InlineData("Mat.25.33, 34")]
    [InlineData("Röm 8 \t\n x@")]
    public void Tokenize_TokensCoverInputContiguously(string input)
    {
        var tokens = ScriptureTokenizer.Tokenize(input);

        var position = 0;
        foreach (var token in tokens)
        {
            token.Start.ShouldBe(position);
            input.Substring(token.Start, token.Length).ShouldBe(token.Text);
            position += token.Length;
        }

        position.ShouldBe(input.Length);
    }

    private static ScriptureToken Tok(ScriptureTokenKind kind, string text, int start) =>
        new(kind, text, start, text.Length);
}
