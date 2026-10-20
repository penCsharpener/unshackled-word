using UnshackledWord.Domain.Models.Study;

namespace UnshackledWord.Application.Repositories;

public interface ISblTextRepository
{
    /// <summary>SBL Greek New Testament verses with LxxRefId between start and end (inclusive), ordered by reference.</summary>
    Task<IReadOnlyList<SblVerse>> GetByRefIdRangeAsync(int startRefId, int endRefId, int limit, CancellationToken token = default);
}
