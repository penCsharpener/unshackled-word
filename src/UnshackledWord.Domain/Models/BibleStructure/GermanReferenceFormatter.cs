namespace UnshackledWord.Domain.Models.BibleStructure;

/// <summary>Formats reference ids in German notation, e.g. "Joh 3,16", "1Joh 4,9-10".</summary>
public static class GermanReferenceFormatter
{
    public static string Format(int refId)
    {
        var r = BibleReference.FromRefId(refId);
        return $"{GermanBookNames.GetAbbreviation(r.BookId)} {r.Chapter},{r.Verse}";
    }

    public static string Format(int startRefId, int? endRefId)
    {
        if (endRefId is null || endRefId == startRefId)
        {
            return Format(startRefId);
        }

        var s = BibleReference.FromRefId(startRefId);
        var e = BibleReference.FromRefId(endRefId.Value);
        if (s.BookId != e.BookId)
        {
            return $"{Format(startRefId)} - {Format(endRefId.Value)}";
        }

        return s.Chapter == e.Chapter
            ? $"{Format(startRefId)}-{e.Verse}"
            : $"{Format(startRefId)}-{e.Chapter},{e.Verse}";
    }
}
