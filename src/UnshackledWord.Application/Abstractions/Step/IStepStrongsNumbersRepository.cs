using UnshackledWord.Domain.Models.Dbo.Step;

namespace UnshackledWord.Application.Abstractions.Step;

public interface IStepStrongsNumbersRepository
{
    Task<int> CountByFilterAsync(CancellationToken token = default);
    Task BulkInsertInternalNewAsync(StrongsNumberDbo[] entries, CancellationToken token = default);
}
