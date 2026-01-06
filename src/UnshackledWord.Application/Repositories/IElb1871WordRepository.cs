using UnshackledWord.Domain.Models.Dbo;

namespace UnshackledWord.Application.Repositories;

public interface IElb1871WordRepository
{
    Task<IEnumerable<Elb1871WordDbo>> GetWordForVerseAsync(int bookId, int chapterId, int verseId, CancellationToken token = default);
    Task<IEnumerable<Elb1871WordDbo>> GetWordForChapterAsync(int bookId, int chapterId, CancellationToken token = default);
    Task<List<int>> BulkUpdateStrongsAsync(List<Elb1871WordDbo> modifiedWords, CancellationToken token = default);
    Task BulkUpdateGrammarAsync(IEnumerable<Elb1871WordDbo> modifiedWords, CancellationToken token = default);
}
