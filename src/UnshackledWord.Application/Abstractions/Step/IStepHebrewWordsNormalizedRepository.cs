using UnshackledWord.Domain.Models.Dbo.Step;

namespace UnshackledWord.Application.Abstractions.Step;

public interface IStepHebrewWordsNormalizedRepository
{
    Task<int> CountByFilterAsync(StepNormalizedHebrewWordsFilter filter, CancellationToken token = default);
    Task BulkInsertAsync(StepHebrewWordsNormalizedDbo[] entries, CancellationToken token = default);
    Task BulkInsertAsync(StepHebrewWordsNormalizedToHebrewWordDbo[] entries, CancellationToken token = default);
}
