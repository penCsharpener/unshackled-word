using UnshackledWord.Domain.Models.Dbo.Step;

namespace UnshackledWord.Application.Abstractions.Step;

public interface IStepHebrewWordsRepository
{
    Task<int> CountByFilterAsync(StepHebrewWordFilter filter, CancellationToken token = default);
    Task<IEnumerable<StepHebrewWordDbo>> GetByFilterAsync(StepHebrewWordFilter filter, CancellationToken token = default);
    Task BulkInsertAsync(ICollection<StepHebrewWordDbo> entries, CancellationToken token = default);
}
