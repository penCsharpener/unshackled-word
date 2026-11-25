using UnshackledWord.Domain.Models.Study;

namespace UnshackledWord.Application.Repositories;

public interface IGreekEditionRepository
{
    /// <summary>The whole catalog of Greek text editions (roadmap 0025), by sort.</summary>
    Task<IReadOnlyList<GreekEditionRow>> GetCatalogAsync(CancellationToken token = default);

    /// <summary>
    /// One query per chapter: the Greek words of the chapter that are not in every main edition or have a shift (same rule as
    /// <c>ReadingEditionsBuilder</c>). Words without any edition row (not imported) are not returned.
    /// </summary>
    Task<IReadOnlyList<ChapterWordEditionMarkRow>> GetChapterEditionMarksAsync(int bibleBookId, int chapter, CancellationToken token = default);
}
