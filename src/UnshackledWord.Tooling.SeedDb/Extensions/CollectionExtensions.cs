namespace UnshackledWord.Tooling.SeedDb.Extensions;

public static class CollectionExtensions
{
    public static async Task<List<T>> ToList<T>(this IAsyncEnumerable<T> stream)
    {
        var list = new List<T>();

        await foreach (var item in stream)
        {
            list.Add(item);
        }

        return list;
    }
}
