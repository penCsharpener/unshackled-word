using UnshackledWord.Domain.Models.Dbo.Step;

namespace UnshackledWord.Application.Abstractions.Step;

public interface IStepHebrewMorphologyRepository
{
    Task<int> CountByFilterAsync(StepHebrewMorphologyFilter filter, CancellationToken token = default);
    Task BulkInsertAsync(StepHebrewMorphologyDbo[] entries, CancellationToken token = default);
}
