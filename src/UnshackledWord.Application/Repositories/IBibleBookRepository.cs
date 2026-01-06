using UnshackledWord.Domain.Models.Dbo;

namespace UnshackledWord.Application.Repositories;

public interface IBibleBookRepository
{
    Task<IEnumerable<BibleBookDbo>> GetBibleBooksAsync(int languageId, CancellationToken token = default);
}
