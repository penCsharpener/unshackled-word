using UnshackledWord.Domain.Models.Dbo.Step;

namespace UnshackledWord.Application.Abstractions.Step;

public interface IStepGreekMorphologyRepository
{
    Task<int> CountByFilterAsync(StepGreekMorphologyFilter filter, CancellationToken token = default);
    Task BulkInsertAsync(StepGreekMorphologyDbo[] entries, CancellationToken token = default);
}
