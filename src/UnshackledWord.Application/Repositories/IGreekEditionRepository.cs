using UnshackledWord.Domain.Models.Study;

namespace UnshackledWord.Application.Repositories;

public interface IGreekEditionRepository
{
    /// <summary>The whole catalog of Greek text editions (roadmap 0025), by sort.</summary>
    Task<IReadOnlyList<GreekEditionRow>> GetCatalogAsync(CancellationToken token = default);
}
