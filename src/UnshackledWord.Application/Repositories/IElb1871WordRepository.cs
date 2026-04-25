using UnshackledWord.Domain.Models.Dbo;
using UnshackledWord.Domain.Models.Dto;

namespace UnshackledWord.Application.Repositories;

public interface IElb1871WordRepository
{
    Task<List<Elb1871WordDbo>> GetWordForVerseAsync(int bookId, int chapterId, int verseId, CancellationToken token = default);
    Task<List<Elb1871WordDbo>> GetWordForChapterAsync(int bookId, int chapterId, CancellationToken token = default);
}
