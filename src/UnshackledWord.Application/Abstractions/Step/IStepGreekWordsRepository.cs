using UnshackledWord.Domain.Models.Dbo.Step;

namespace UnshackledWord.Application.Abstractions.Step;

public interface IStepGreekWordsRepository
{
    Task<int> CountByFilterAsync(StepGreekWordFilter filter, CancellationToken token = default);
    Task<IEnumerable<StepGreekWordDbo>> GetByFilterAsync(StepGreekWordFilter filter, CancellationToken token = default);
    /// <summary>Inserts the words into an empty table; returns the rows written (0 when the table already had words).</summary>
    Task<int> BulkInsertAsync(ICollection<StepGreekWordDbo> entries, CancellationToken token = default);
    Task<int> RefreshEditionsAsync(CancellationToken token = default);

    /// <summary>True when there are Greek words but no word-edition rows (roadmap 0025), e.g. after an interrupted refresh.</summary>
    Task<bool> EditionsMissingAsync(CancellationToken token = default);
}
