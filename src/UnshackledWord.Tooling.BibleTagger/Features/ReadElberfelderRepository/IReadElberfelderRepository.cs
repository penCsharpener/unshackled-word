using UnshackledWord.Domain.WebApi.BibleTagger.Reading;

namespace UnshackledWord.Tooling.BibleTagger.Features.ReadElberfelderRepository;

public interface IReadElberfelderRepository
{
    Task<GetWordsOfChapterResponse> GetWordsInChapterAsync(int bookId, int chapter, CancellationToken token = default);
}
