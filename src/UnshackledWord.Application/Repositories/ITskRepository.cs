using UnshackledWord.Domain.Models.Study;

namespace UnshackledWord.Application.Repositories;

public interface ITskRepository
{
    /// <summary>
    /// TSK cross-references that start at verses in the given Elberfelder (HebRefId) range, inclusive.
    /// Source and targets are translated to HebRefId. Ordered by source verse, then TSK order.
    /// </summary>
    Task<IReadOnlyList<TskCrossReference>> GetOutgoingAsync(int startHebRefId, int endHebRefId, int limit, CancellationToken token = default);
}
