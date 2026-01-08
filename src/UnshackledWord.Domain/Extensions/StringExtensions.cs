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
}
