using UnshackledWord.Domain.Models.Dbo.Step;

namespace UnshackledWord.Application.Abstractions.Step;

public interface IStepStrongsToVersesRepository
{
    Task<int> CountByFilterAsync(StepStrongsFilter filter, CancellationToken token = default);
    Task BulkInsertAsync(StepStrongsToVersesDbo[] entries, CancellationToken token = default);
}
