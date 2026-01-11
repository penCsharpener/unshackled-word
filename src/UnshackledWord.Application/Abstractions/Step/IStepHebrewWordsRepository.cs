using UnshackledWord.Domain.Models.Dbo.Step;

namespace UnshackledWord.Application.Abstractions.Step;

public interface IStepHebrewWordsRepository
{
    Task<int> CountByFilterAsync(StepHebrewWordFilter filter, CancellationToken token = default);
    Task BulkInsertAsync(StepHebrewWordDbo[] entries, CancellationToken token = default);
}
