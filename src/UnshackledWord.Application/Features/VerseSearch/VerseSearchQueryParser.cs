using System.Text.RegularExpressions;

namespace UnshackledWord.Application.Features.VerseSearch;

/// <summary>One searched or excluded unit of a query: a single word, or the words of a "quoted phrase".</summary>
public sealed record SearchTerm(IReadOnlyList<string> Words, bool IsPhrase, bool Negated);

/// <summary>A parsed query: alternatives (separated by oder / or) of AND-ed terms. Alternatives without a positive term are dropped.</summary>
public sealed class VerseSearchQuery
{
    public IReadOnlyList<IReadOnlyList<SearchTerm>> Groups { get; init; } = [];

    /// <summary>All words of the input including excluded ones (what the 8-word limit counts).</summary>
    public int WordCount { get; init; }

    public bool HasPositive => Groups.Count > 0;

    public IEnumerable<string> AllWords => Groups.SelectMany(g => g).SelectMany(t => t.Words);
}

/// <summary>
/// Parses the smart-search box: words are AND-ed, "a b" is a phrase, a leading - excludes, oder / or separates alternatives.
/// Pure text work; the result is turned into a tsquery by <see cref="VerseSearchQueryBuilder"/>, so user text never reaches SQL.
/// </summary>
public static partial class VerseSearchQueryParser
{
    [GeneratedRegex(@"\p{L}+(?:['’-]\p{L}+)*")]
    private static partial Regex WordRegex();

    public static VerseSearchQuery Parse(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return new VerseSearchQuery();
        }

        var text = input.Trim();
        var groups = new List<List<SearchTerm>>();
        var current = new List<SearchTerm>();
        var wordCount = 0;
        var i = 0;

        while (i < text.Length)
        {
            if (char.IsWhiteSpace(text[i]))
            {
                i++;
                continue;
            }

            var negated = false;
            if (text[i] == '-' && i + 1 < text.Length && (text[i + 1] == '"' || char.IsLetter(text[i + 1])))
            {
                negated = true;
                i++;
            }

            if (text[i] == '"')
            {
                var end = text.IndexOf('"', i + 1);
                if (end < 0)
                {
                    end = text.Length;
                }

                var words = Words(text[(i + 1)..end]);
                i = Math.Min(end + 1, text.Length);
                if (words.Count > 0)
                {
                    wordCount += words.Count;
                    current.Add(new SearchTerm(words, words.Count > 1, negated));
                }

                continue;
            }

            var start = i;
            while (i < text.Length && !char.IsWhiteSpace(text[i]) && text[i] != '"')
            {
                i++;
            }

            var run = text[start..i];
            if (!negated && IsOr(run))
            {
                CloseGroup(groups, ref current);
                continue;
            }

            foreach (var word in Words(run))
            {
                wordCount++;
                current.Add(new SearchTerm([word], false, negated));
            }
        }

        CloseGroup(groups, ref current);

        return new VerseSearchQuery
        {
            Groups = groups
                .Where(g => g.Any(t => !t.Negated))
                .Select(g => (IReadOnlyList<SearchTerm>)g)
                .ToList(),
            WordCount = wordCount
        };
    }

    private static void CloseGroup(List<List<SearchTerm>> groups, ref List<SearchTerm> current)
    {
        if (current.Count > 0)
        {
            groups.Add(current);
            current = [];
        }
    }

    private static bool IsOr(string run)
    {
        return run.Equals("oder", StringComparison.OrdinalIgnoreCase) || run.Equals("or", StringComparison.OrdinalIgnoreCase);
    }

    private static List<string> Words(string text)
    {
        return WordRegex().Matches(text)
            .Select(m => m.Value.ToLowerInvariant().Replace('’', '\''))
            .ToList();
    }
}
