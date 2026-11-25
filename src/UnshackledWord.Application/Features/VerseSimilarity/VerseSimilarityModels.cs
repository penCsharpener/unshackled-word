namespace UnshackledWord.Application.Features.VerseSimilarity;

/// <summary>Which corpus a verse belongs to. Each testament is scored on its own (roadmap 0027, 03-algorithm.md).</summary>
public enum Testament
{
    Old,
    New,
}

/// <summary>One verse as the sequence of its Strong's numbers in word order (Greek <c>G1234A</c>, Hebrew root pieces <c>H0430G</c>).</summary>
/// <param name="VerseId">STEP key (<c>LxxRefId</c>).</param>
public sealed record VerseTokenSequence(int VerseId, IReadOnlyList<string> Strongs);

/// <summary>
/// Scoring parameters (roadmap 0027). <see cref="StopDfShare"/>: a Strong's in more than this share of the testament's verses is a stop word.
/// <see cref="TopK"/>: hits kept per source verse. <see cref="MinScore"/>: lowest score kept. <see cref="PhraseWeight"/>: weight of the bigram bonus.
/// </summary>
public sealed record VerseSimilarityParameters(double StopDfShare = 0.02, int TopK = 30, double MinScore = 0.1, double PhraseWeight = 0.5)
{
    public static VerseSimilarityParameters Default { get; } = new();
}

/// <summary>One ranked hit: <see cref="TargetVerseId"/> is the <see cref="Rank"/>-th most similar verse of <see cref="SourceVerseId"/> (1-based).</summary>
/// <param name="SharedCount">Distinct content Strong's in both verses.</param>
/// <param name="PhraseCount">Distinct content bigrams (stop words removed) in both verses, in the same order.</param>
public readonly record struct VerseSimilarityHit(int SourceVerseId, int TargetVerseId, int Rank, double Score, int SharedCount, int PhraseCount);

/// <summary>A shared content Strong's with its idf.</summary>
public sealed record SharedStrongs(string Strongs, double Idf);

/// <summary>Why two verses match: the shared content Strong's (rarest first) and the matched runs (each at least two Strong's, in the source's order).</summary>
public sealed record VerseSimilarityExplanation(int SourceVerseId, int TargetVerseId, double Score, IReadOnlyList<SharedStrongs> Shared, IReadOnlyList<IReadOnlyList<string>> Phrases);

/// <summary>A cut Strong's with its document frequency.</summary>
public sealed record StopStrongs(string Strongs, int DocumentFrequency);
