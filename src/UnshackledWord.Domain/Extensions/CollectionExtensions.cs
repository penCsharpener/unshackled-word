using System.Diagnostics.CodeAnalysis;

namespace UnshackledWord.Domain.Extensions;

public static class CollectionExtensions
{
    public static IList<T> AddIfNotNull<T>(this IList<T> list, T? value) where T : class
    {
        if (value is null)
        {
            return list;
        }

        list.Add(value);
        return list;
    }
    
    public static IList<T> AddRangeIfNotNull<T>(this IList<T> list, IEnumerable<T>? values) where T : class
    {
        if (values is null)
        {
            return list;
        }

        foreach (var value in values)
        {
            list.Add(value);
        }

        return list;
    }
    
    public static bool IsNullOrEmpty<T>([NotNullWhen(false)] this IEnumerable<T>? enumerable)
    {
        return enumerable is null || !enumerable.Any();
    }
    
    public static bool IsNotNullOrEmpty<T>([NotNullWhen(true)] this IEnumerable<T>? enumerable)
    {
        return !enumerable.IsNullOrEmpty();
    }
}
