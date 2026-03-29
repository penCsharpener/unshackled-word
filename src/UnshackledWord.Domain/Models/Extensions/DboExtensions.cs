namespace UnshackledWord.Domain.Models.Extensions;

public static class DboExtensions
{
    public static IEnumerable<T> SortByBibleOrder<T>(this IEnumerable<T> source) where T : IBibleWordOrderColumns
    {
        return source.OrderBy(x => x.LxxRefId)
            .ThenBy(x => x.PositionInVerse);
    }

    public static IEnumerable<T> EnumerateWithIds<T>(this IEnumerable<T> source) where T : IEntityId
    {
        int id = 1;
        foreach (var item in source)
        {
            item!.Id = id++;
            yield return item;
        }
    }

}
