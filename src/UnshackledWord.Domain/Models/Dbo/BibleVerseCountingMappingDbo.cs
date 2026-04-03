using UnshackledWord.Domain.Models.BibleStructure;

namespace UnshackledWord.Domain.Models.Dbo;

public sealed class BibleVerseCountingMappingDbo
{
    public const string DboName = "\"unshackled-word\".\"BibleVerseCountingMapping\"";

    public int Id { get; set; }
    public int HebRefId { get; set; }
    public int LxxRefId { get; set; }
}

public static class BibleVerseCountingMappingExtensions
{
    public static BibleReference GetFirstReference(this IEnumerable<BibleVerseCountingMappingDbo> items)
    {
        var first = items.OrderBy(x => x.HebRefId).First();
        return BibleReference.FromRefId(first.HebRefId, first.LxxRefId);
    }

    public static BibleReference GetLastReference(this IEnumerable<BibleVerseCountingMappingDbo> items)
    {
        var last = items.OrderByDescending(x => x.HebRefId).First();
        return BibleReference.FromRefId(last.HebRefId, last.LxxRefId);
    }
}
