using UnshackledWord.Domain.Models.Dbo.Step;

namespace UnshackledWord.Application.Abstractions.Step;

public interface IStepStrongsRepository
{
    Task<int> CountByFilterAsync(StepStrongsFilter filter, CancellationToken token = default);
    Task BulkInsertAsync(StepStrongsDbo[] entries, CancellationToken token = default);
}
