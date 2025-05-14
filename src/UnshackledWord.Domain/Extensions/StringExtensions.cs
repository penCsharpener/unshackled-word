namespace UnshackledWord.Domain.Extensions;

public static class StringExtensions
{
    public static string JoinStrings(this IEnumerable<string> elements, string separator)
    {
        return string.Join(separator, elements);
    }

    public static bool IsNullOrWhiteSpace(this string? value)
    {
        return string.IsNullOrWhiteSpace(value);
    }

    public static bool IsNullOrEmpty(this string? value)
    {
        return string.IsNullOrEmpty(value);
    }

    public static bool IsNotNullOrWhiteSpace(this string? value)
    {
        return string.IsNullOrWhiteSpace(value) is false;
    }

    public static bool IsNotNullOrEmpty(this string? value)
    {
        return string.IsNullOrEmpty(value) is false;
    }
}
