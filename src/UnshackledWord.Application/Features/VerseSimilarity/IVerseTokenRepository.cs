namespace UnshackledWord.Application.Features.VerseSimilarity;

/// <summary>Lemma and gloss of one Strong's number from the STEP lexicon (for explanations).</summary>
public sealed record StrongsLabel(string Strongs, string Lemma, string Gloss, string Transliteration = "");

/// <summary>Reads the Strong's token sequences per verse from the STEP tables (roadmap 0027, 03-algorithm.md "Tokens").</summary>
public interface IVerseTokenRepository
{
    /// <summary>
    /// Every verse of the testament with its Strong's in word order, keyed by <c>LxxRefId</c>. Greek: <c>DisambiguatedStrongs</c> of the
    /// words in at least one main edition. Hebrew/Aramaic: the root pieces of the normalized words (prefix and suffix pieces dropped).
    /// </summary>
    Task<IReadOnlyList<VerseTokenSequence>> LoadAsync(Testament testament, CancellationToken token = default);

    /// <summary>Lemma and gloss per Strong's (<c>G0886</c>, <c>H0430G</c>); unknown numbers are missing from the result.</summary>
    Task<IReadOnlyDictionary<string, StrongsLabel>> GetLabelsAsync(IReadOnlyCollection<string> strongs, CancellationToken token = default);

    /// <summary>The STEP key (<c>LxxRefId</c>) of an Elberfelder verse (<c>HebRefId</c>), or null when there is none.</summary>
    Task<int?> FindLxxRefIdAsync(int hebRefId, CancellationToken token = default);
}
