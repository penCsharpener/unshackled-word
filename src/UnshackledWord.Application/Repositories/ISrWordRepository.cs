using UnshackledWord.Domain.Models.Dbo;

namespace UnshackledWord.Application.Repositories;

public interface ISrWordRepository
{
    Task<List<SrGntWordDbo>> GetWordForVerseAsync(int bookId, int chapterId, int verseId, CancellationToken token = default);
    Task<List<SrGntWordDbo>> GetWordForChapterAsync(int bookId, int chapterId, CancellationToken token = default);
}
