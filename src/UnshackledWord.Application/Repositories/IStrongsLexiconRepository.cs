using UnshackledWord.Domain.Models.Study;

namespace UnshackledWord.Application.Repositories;

public interface IStrongsLexiconRepository
{
    /// <summary>Lexicon entries for a Strong's number. Without Extra all variants (A, B, ...) are returned.</summary>
    Task<IReadOnlyList<LexiconEntry>> GetEntriesAsync(StrongsNumber strongs, CancellationToken token = default);
}
