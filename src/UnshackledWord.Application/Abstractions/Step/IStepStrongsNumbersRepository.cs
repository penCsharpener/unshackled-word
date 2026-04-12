using UnshackledWord.Domain.Models.Dbo.Step;

namespace UnshackledWord.Application.Abstractions.Step;

public interface IStepStrongsNumbersRepository
{
    Task<int> CountByFilterAsync(StrongsLanguage language, CancellationToken token = default);
    Task BulkInsertInternalNewAsync(ICollection<StepStrongsToTextDbo> entries, CancellationToken token = default);
}
