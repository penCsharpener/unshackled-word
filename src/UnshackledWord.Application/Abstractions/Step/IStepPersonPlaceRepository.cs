using UnshackledWord.Domain.Models.Dbo.Step;

namespace UnshackledWord.Application.Abstractions.Step;

public interface IStepPersonPlaceRepository
{
    Task<int> CountPersonsByFilterAsync(StepPersonLexiconFilter filter, CancellationToken token = default);
    Task<int> CountPersonRelationsByFilterAsync(StepPersonLexiconFilter filter, CancellationToken token = default);
    Task<int> CountPlacesByFilterAsync(StepPersonLexiconFilter filter, CancellationToken token = default);
    Task<int> CountOthersByFilterAsync(StepPersonLexiconFilter filter, CancellationToken token = default);
    Task BulkInsertAsync(StepPersonLexiconDbo[] entries, CancellationToken token = default);
    Task BulkInsertAsync(StepPersonLexiconRelationsDbo[] entries, CancellationToken token = default);
    Task BulkInsertAsync(StepPlaceLexiconDbo[] entries, CancellationToken token = default);
    Task BulkInsertAsync(StepOtherLexiconDbo[] entries, CancellationToken token = default);
}
