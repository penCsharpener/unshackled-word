using UnshackledWord.Domain.Models.Study;

namespace UnshackledWord.Application.Repositories;

public interface IWordDetailRepository
{
    Task<ElbWordRow?> GetWordAsync(int wordId, CancellationToken token = default);

    /// <summary>Mapping rows of a German word in the NT, joined to the Greek word and its STEP morphology. Added-word rows have no StepWordId.</summary>
    Task<IReadOnlyList<GreekReadingRow>> GetGreekReadingsAsync(int wordId, CancellationToken token = default);

    /// <summary>Mapping rows of a German word in the OT, joined to the Hebrew/Aramaic word.</summary>
    Task<IReadOnlyList<HebrewReadingRow>> GetHebrewReadingsAsync(int wordId, CancellationToken token = default);

    /// <summary>Other German words that share a mapping with this word: same original word, added word (child) or parent.</summary>
    Task<IReadOnlyList<CoMappedWordRow>> GetCoMappedWordsAsync(int wordId, bool hebrew, CancellationToken token = default);
}
