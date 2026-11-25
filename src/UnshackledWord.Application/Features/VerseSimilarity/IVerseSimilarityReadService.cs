using UnshackledWord.Domain.WebApi.Elberfelder.GetSimilarVerses;

namespace UnshackledWord.Application.Features.VerseSimilarity;

/// <summary>Reads the stored similar verses and explains them at read time (roadmap 0027.3).</summary>
public interface IVerseSimilarityReadService
{
    /// <summary>Every verse of the chapter with up to <paramref name="limit"/> hits (1–30).</summary>
    Task<GetSimilarVersesResponse> GetChapterAsync(int bibleBookId, int chapter, int limit, CancellationToken token = default);

    /// <summary>Up to 30 hits of one verse, or null when the Elberfelder verse does not exist.</summary>
    Task<VerseSimilarVersesResponse?> GetVerseAsync(int hebRefId, CancellationToken token = default);
}
