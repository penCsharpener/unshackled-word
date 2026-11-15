using System.Text.RegularExpressions;
using UnshackledWord.Domain.Extensions;

namespace UnshackledWord.Application.Features.Revision;

/// <summary>One word of a revised verse. <see cref="PlainWord"/> follows <see cref="Elb1871PlainWord"/> (the parser's rule), empty when only punctuation remains.</summary>
public sealed record RevisionToken(int Position, string WordInContext, string? PlainWord);

/// <summary>Splits verse text into words exactly like the Elberfelder parser does (whitespace-separated, <c>PlainWord</c> by the shared rule).</summary>
public static partial class RevisionTokenizer
{
    public static List<RevisionToken> Tokenize(string? text)
    {
        var tokens = new List<RevisionToken>();
        if (string.IsNullOrWhiteSpace(text))
        {
            return tokens;
        }

        var collapsed = WhitespaceRegex().Replace(text, " ").Trim();
        var position = 1;

        foreach (var word in collapsed.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            tokens.Add(new RevisionToken(position, word, Elb1871PlainWord.From(word)));
            position++;
        }

        return tokens;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
