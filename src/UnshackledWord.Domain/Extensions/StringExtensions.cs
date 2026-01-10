using System.Diagnostics.CodeAnalysis;

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

}
