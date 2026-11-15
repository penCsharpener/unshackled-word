using UnshackledWord.Domain.Models.Study;

namespace UnshackledWord.Application.Features.Revision;

/// <summary>A word of a revision variant as the word detail needs it.</summary>
public sealed class RevisionWordHeaderRow
{
    public int Id { get; init; }
    public int BibleBookId { get; init; }
    public int Chapter { get; init; }
    public int Verse { get; init; }
    public int HebRefId { get; init; }
    public int PositionInVerse { get; init; }
    public string WordInContext { get; init; } = default!;
    public int Level { get; init; }
    public string Change { get; init; } = default!;

    /// <summary>Lemma from Elb1871Morphology, only filled for <c>same</c> words (the word is the Elb1871 word).</summary>
    public string? GermanLemma { get; init; }

    /// <summary>The Elb1871 word a <c>changed</c> word replaces.</summary>
    public string? BaseWord { get; init; }
}

public interface IRevisionWordDetailRepository
{
    Task<RevisionWordHeaderRow?> GetWordAsync(int revisionWordId, CancellationToken token = default);
    Task<IReadOnlyList<GreekReadingRow>> GetGreekReadingsAsync(int revisionWordId, CancellationToken token = default);
    Task<IReadOnlyList<HebrewReadingRow>> GetHebrewReadingsAsync(int revisionWordId, CancellationToken token = default);

    /// <summary>Other words of the same variant sharing an original word, added children and the parent; ids are RevisionWords ids.</summary>
    Task<IReadOnlyList<CoMappedWordRow>> GetCoMappedWordsAsync(int revisionWordId, CancellationToken token = default);
}
