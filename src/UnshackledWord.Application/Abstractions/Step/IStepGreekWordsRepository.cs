using UnshackledWord.Domain.Models.Dbo.Step;

namespace UnshackledWord.Application.Abstractions.Step;

public interface IStepGreekWordsRepository
{
    Task<int> CountByFilterAsync(StepGreekWordFilter filter, CancellationToken token = default);
    Task<IEnumerable<StepGreekWordDbo>> GetByFilterAsync(StepGreekWordFilter filter, CancellationToken token = default);
    Task BulkInsertAsync(StepGreekWordDbo[] entries, CancellationToken token = default);
}
