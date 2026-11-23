using UnshackledWord.Domain.WebApi.Elberfelder.GetChapterCrossReferences;

namespace UnshackledWord.Application.Features.CrossReferences;

public interface IChapterCrossReferenceService
{
    /// <param name="includeHidden">Curators only (the endpoint checks the role): hidden links come back with rating <c>hide</c>.</param>
    Task<GetChapterCrossReferencesResponse> GetAsync(int bibleBookId, int chapter, bool includeHidden = false, CancellationToken token = default);
}
