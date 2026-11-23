using UnshackledWord.Domain.Models.BibleStructure;
using UnshackledWord.Domain.Models.Study;
using UnshackledWord.Domain.WebApi.Elberfelder.GetWordDetail;
using UnshackledWord.Domain.WebApi.Tsk;

namespace UnshackledWord.Application.Features.CrossReferences;

public static class CrossReferenceMapper
{
    /// <summary>A few TSK ranges run backwards (Heb 9:21-15) and some collapse to one verse; both are normalised, ranges across chapters are cut to their first verse.
    /// Since migration 0044 (roadmap 0024.2) the stored keys are already normalised this way (<c>TskKeySql</c>), so this is a no-op on them;
    /// it stays the C# reference the SQL is tested against.</summary>
    public static CrossReferenceResponse Build(TskCrossReference row)
    {
        var start = BibleReference.FromRefId(row.TargetStartHebRefId);
        var reference = new CrossReferenceResponse
        {
            BibleBookId = start.BookId, Chapter = start.Chapter, Verse = start.Verse, Rating = row.Rating, Comment = row.Comment,
            Origin = row.Origin == TskOrigins.Added ? TskOrigins.Added : null,
        };
        if (row.TargetEndHebRefId is not { } endRefId || endRefId == row.TargetStartHebRefId)
        {
            return reference;
        }

        var end = BibleReference.FromRefId(endRefId);
        if (end.BookId != start.BookId || end.Chapter != start.Chapter)
        {
            return reference;
        }

        reference.Verse = Math.Min(start.Verse, end.Verse);
        reference.EndVerse = Math.Max(start.Verse, end.Verse);
        return reference;
    }
}
