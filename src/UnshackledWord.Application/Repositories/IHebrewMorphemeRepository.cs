using UnshackledWord.Domain.Models.Study;

namespace UnshackledWord.Application.Repositories;

public interface IHebrewMorphemeRepository
{
    Task<IReadOnlyList<HebrewMorpheme>> GetByStepWordIdsAsync(int[] stepHebrewWordIds, CancellationToken token = default);
}
