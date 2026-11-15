using UnshackledWord.Domain.WebApi.Elberfelder.GetChapterOriginals;

namespace UnshackledWord.Application.Features.ChapterOriginals;

public interface IChapterOriginalsService
{
    Task<GetChapterOriginalsResponse> GetAsync(int bibleBookId, int chapter, CancellationToken token = default);
}
