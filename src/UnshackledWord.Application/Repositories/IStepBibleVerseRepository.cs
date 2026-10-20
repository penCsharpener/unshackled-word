using UnshackledWord.Domain.Models.Study;

namespace UnshackledWord.Application.Repositories;

public interface IStepBibleVerseRepository
{
    /// <summary>STEP original-language verses (Hebrew OT, Greek NT) with HebRefId between start and end (inclusive), ordered by reference.</summary>
    Task<IReadOnlyList<StepBibleVerse>> GetByHebRefIdRangeAsync(int startRefId, int endRefId, int limit, CancellationToken token = default);
}
