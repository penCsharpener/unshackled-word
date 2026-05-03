using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

namespace UnshackledWord.Domain.Extensions;

public static class StringExtensions
{
    public static string JoinStrings<T>(this IEnumerable<T> elements, string separator)
    {
        return string.Join(separator, elements);
    }

    public static bool IsNullOrWhiteSpace([NotNullWhen(false)] this string? value)
    {
        return string.IsNullOrWhiteSpace(value);
    }

    public static bool IsNullOrEmpty([NotNullWhen(false)] this string? value)
    {
        return string.IsNullOrEmpty(value);
    }

    public static bool IsNotNullOrWhiteSpace([NotNullWhen(true)] this string? value)
    {
        return string.IsNullOrWhiteSpace(value) is false;
    }

    public static bool IsNotNullOrEmpty([NotNullWhen(true)] this string? value)
    {
        return string.IsNullOrEmpty(value) is false;
    }

    public static string? SetNullOrValue(this string value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    public static IList<string> AddIfNotNull(this IList<string> list, string? value)
    {
        if (value.IsNullOrWhiteSpace())
        {
            return list;
        }

        list.Add(value);
        return list;
    }

    public static IEnumerable<string> SplitIgnoringParentheses(this string input, char delimiter, StringSplitOptions options = StringSplitOptions.None)
    {
        var nestingLevel = 0;
        var startIndex = 0;

        for (var i = 0; i < input.Length; i++)
        {
            if (input[i] == '(')
            {
                nestingLevel++;
            }
            else if (input[i] == ')')
            {
                nestingLevel--;
            }
            else if (input[i] == delimiter && nestingLevel == 0)
            {
                var segment = input.Substring(startIndex, i - startIndex);
                startIndex = i + 1;

                // Apply StringSplitOptions here
                if (options.HasFlag(StringSplitOptions.TrimEntries))
                {
                    segment = segment.Trim();
                }

                if (options.HasFlag(StringSplitOptions.RemoveEmptyEntries) && string.IsNullOrWhiteSpace(segment))
                {
                    continue;
                }

                yield return segment;
            }
        }

        if (startIndex >= input.Length)
        {
            yield break;
        }

        var lastSegment = input.Substring(startIndex);

        // Apply StringSplitOptions for the last segment
        if (options.HasFlag(StringSplitOptions.TrimEntries))
        {
            lastSegment = lastSegment.Trim();
        }

        if (options.HasFlag(StringSplitOptions.RemoveEmptyEntries) && string.IsNullOrWhiteSpace(lastSegment))
        {
            yield break;
        }

        yield return lastSegment;
    }

    public static string? RemoveGreekDiacritics(this string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        // Step 1: Decompose characters (e.g., 'ᾄ' becomes 'α' + breathing + accent + subscript)
        var normalizedString = text.Normalize(NormalizationForm.FormD);
        var stringBuilder = new StringBuilder();

        foreach (var c in normalizedString)
        {
            // Step 2: Only keep characters that are NOT non-spacing marks
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category != UnicodeCategory.NonSpacingMark && category != UnicodeCategory.OtherPunctuation)
            {
                stringBuilder.Append(c);
            }
        }

        // Step 3: Recompose into a standard form (Form C)
        return stringBuilder.ToString().Normalize(NormalizationForm.FormC);
    }

    public static string? RemoveHebrewDiacritics(this string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        // NormalizationForm.FormD separates consonants from vowels/accents
        var normalizedString = text.Normalize(NormalizationForm.FormD);
        var stringBuilder = new StringBuilder();

        foreach (var c in normalizedString)
        {
            // Filter out all "Non-Spacing Marks" (vowels, accents, dagesh, etc.)
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category != UnicodeCategory.NonSpacingMark && category != UnicodeCategory.OtherPunctuation)
            {
                stringBuilder.Append(c);
            }
        }

        // Return to standard composed form
        return stringBuilder.ToString().Normalize(NormalizationForm.FormC);
    }

    public static string RemovePunctuation(this string word)
    {
        var characters = ",;:.!?\"'{}[]()’¶…".ToCharArray();

        var result = word.Trim();

        foreach (var character in characters)
        {
            result = result.Replace(character.ToString(), string.Empty);
        }

        result = result.Trim('-');

        return result;
    }
}
