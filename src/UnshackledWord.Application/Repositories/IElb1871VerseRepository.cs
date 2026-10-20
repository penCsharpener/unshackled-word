using UnshackledWord.Domain.Models.Study;

namespace UnshackledWord.Application.Repositories;

public interface IElb1871VerseRepository
{
    /// <summary>Verses with HebRefId between start and end (inclusive), ordered by reference.</summary>
    Task<IReadOnlyList<ElbVerse>> GetByHebRefIdRangeAsync(int startRefId, int endRefId, int limit, CancellationToken token = default);
}
