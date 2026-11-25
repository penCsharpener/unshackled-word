using UnshackledWord.Domain.Models.Dbo.Step;

namespace UnshackledWord.Application.Abstractions.Step;

public interface IStepHebrewWordsRepository
{
    Task<int> CountByFilterAsync(StepHebrewWordFilter filter, CancellationToken token = default);
    Task<IEnumerable<StepHebrewWordDbo>> GetByFilterAsync(StepHebrewWordFilter filter, CancellationToken token = default);
    /// <summary>Inserts the words when the table is empty; returns the number of rows inserted (0 when skipped).</summary>
    Task<int> BulkInsertAsync(ICollection<StepHebrewWordDbo> entries, CancellationToken token = default);
}
