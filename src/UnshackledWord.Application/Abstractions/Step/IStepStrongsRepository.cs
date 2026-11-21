using UnshackledWord.Domain.Models.Dbo.Step;

namespace UnshackledWord.Application.Abstractions.Step;

public interface IStepStrongsRepository
{
    Task<int> CountByFilterAsync(StepStrongsFilter filter, CancellationToken token = default);
    Task<IEnumerable<StepStrongsLexiconDbo>> GetByFilterAsync(StepStrongsFilter filter, CancellationToken token = default);
    Task BulkInsertAsync(StepStrongsLexiconDbo[] entries, CancellationToken token = default);

    /// <summary>
    /// Inserts the entries whose (language, number, extra) key is not in the lexicon yet, with new ids behind the existing ones; existing rows and ids stay.
    /// Returns the keys that were added.
    /// </summary>
    Task<IReadOnlyList<string>> InsertMissingAsync(StepStrongsLexiconDbo[] entries, CancellationToken token = default);
}
