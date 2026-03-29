using UnshackledWord.Domain.Models.Dbo.Step;

namespace UnshackledWord.Application.Abstractions.Step;

public interface IStepStrongsRepository
{
    Task<int> CountByFilterAsync(StepStrongsFilter filter, CancellationToken token = default);
    Task<IEnumerable<StepStrongsLexiconDbo>> GetByFilterAsync(StepStrongsFilter filter, CancellationToken token = default);
    Task BulkInsertAsync(StepStrongsLexiconDbo[] entries, CancellationToken token = default);
}
