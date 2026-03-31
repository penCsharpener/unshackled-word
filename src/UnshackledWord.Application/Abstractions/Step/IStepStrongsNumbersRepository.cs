using UnshackledWord.Domain.Models.Dbo.Step;

namespace UnshackledWord.Application.Abstractions.Step;

public interface IStepStrongsNumbersRepository
{
    Task<int> CountByFilterAsync(CancellationToken token = default);
    Task BulkInsertInternalNewAsync(StepStrongsToTextDbo[] entries, CancellationToken token = default);
}
