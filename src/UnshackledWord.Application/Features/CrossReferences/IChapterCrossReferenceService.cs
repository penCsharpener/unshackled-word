using UnshackledWord.Domain.WebApi.Elberfelder.GetChapterCrossReferences;

namespace UnshackledWord.Application.Features.CrossReferences;

public interface IChapterCrossReferenceService
{
    /// <param name="includeHidden">Curators only (the endpoint checks the role): hidden links come back with rating <c>hide</c>.</param>
    /// <param name="level">Literalness level (roadmap 0024.5): 0 = Elb1871; otherwise ok word links on verses with a variant at that level carry <c>mappedWords</c>.</param>
    Task<GetChapterCrossReferencesResponse> GetAsync(int bibleBookId, int chapter, bool includeHidden = false, int level = 0, CancellationToken token = default);
}
