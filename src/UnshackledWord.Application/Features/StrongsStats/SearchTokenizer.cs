using System.Text.RegularExpressions;

namespace UnshackledWord.Application.Features.StrongsStats;

/// <summary>Splits a smart-search query into lower-cased German word tokens (letters with inner apostrophes or hyphens), duplicates dropped, query order kept.</summary>
public static partial class SearchTokenizer
{
    [GeneratedRegex(@"\p{L}+(?:['’-]\p{L}+)*")]
    private static partial Regex TokenRegex();

    public static IReadOnlyList<string> Tokenize(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        return TokenRegex().Matches(query)
            .Select(m => m.Value.ToLowerInvariant().Replace('’', '\''))
            .Distinct()
            .ToList();
    }
}
