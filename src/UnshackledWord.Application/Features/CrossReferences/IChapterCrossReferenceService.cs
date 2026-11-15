using UnshackledWord.Domain.WebApi.Elberfelder.GetChapterCrossReferences;

namespace UnshackledWord.Application.Features.CrossReferences;

public interface IChapterCrossReferenceService
{
    Task<GetChapterCrossReferencesResponse> GetAsync(int bibleBookId, int chapter, CancellationToken token = default);
}
