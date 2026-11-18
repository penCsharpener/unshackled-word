using System.Text.RegularExpressions;

namespace UnshackledWord.Domain.Models.BibleStructure.References;

/// <summary>Kind of a token in a scripture reference string.</summary>
public enum ScriptureTokenKind
{
    Whitespace,
    Number,
    Word,
    Colon,
    Comma,
    Dot,
    Semicolon,
    Dash,
    Other
}

/// <summary>One token of a reference string. <see cref="Start"/> and <see cref="Length"/> index the original input.</summary>
public sealed record ScriptureToken(ScriptureTokenKind Kind, string Text, int Start, int Length);

/// <summary>
/// Splits reference text into tokens without interpreting them. Book, chapter and verse decisions belong to the parser.
/// Every character lands in exactly one token, so the token texts concatenate to the input.
/// </summary>
public static partial class ScriptureTokenizer
{
    // Word letters include combining marks so decomposed umlauts (o + U+0308) stay in one word.
    // Dash covers hyphen, the Unicode dash block (U+2010-U+2015) and minus sign (U+2212).
    [GeneratedRegex(@"(?<ws>\s+)|(?<num>\d+)|(?<word>[\p{L}\p{M}]+)|(?<colon>:)|(?<comma>,)|(?<dot>\.)|(?<semi>;)|(?<dash>[-‐-―−])|(?<other>.)")]
    private static partial Regex TokenRegex();

    public static IReadOnlyList<ScriptureToken> Tokenize(string? input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return [];
        }

        var tokens = new List<ScriptureToken>();
        foreach (Match match in TokenRegex().Matches(input))
        {
            tokens.Add(new ScriptureToken(KindOf(match), match.Value, match.Index, match.Length));
        }

        return tokens;
    }

    private static ScriptureTokenKind KindOf(Match match)
    {
        if (match.Groups["ws"].Success)
        {
            return ScriptureTokenKind.Whitespace;
        }

        if (match.Groups["num"].Success)
        {
            return ScriptureTokenKind.Number;
        }

        if (match.Groups["word"].Success)
        {
            return ScriptureTokenKind.Word;
        }

        if (match.Groups["colon"].Success)
        {
            return ScriptureTokenKind.Colon;
        }

        if (match.Groups["comma"].Success)
        {
            return ScriptureTokenKind.Comma;
        }

        if (match.Groups["dot"].Success)
        {
            return ScriptureTokenKind.Dot;
        }

        if (match.Groups["semi"].Success)
        {
            return ScriptureTokenKind.Semicolon;
        }

        if (match.Groups["dash"].Success)
        {
            return ScriptureTokenKind.Dash;
        }

        return ScriptureTokenKind.Other;
    }
}
