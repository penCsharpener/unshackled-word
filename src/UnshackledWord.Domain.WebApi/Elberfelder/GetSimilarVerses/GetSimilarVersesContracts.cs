namespace UnshackledWord.Domain.WebApi.Elberfelder.GetSimilarVerses;

/// <summary><c>GET api/elberfelder/similarVerses/{bibleBookId}/{chapter}?limit=10</c> (roadmap 0027.3). <see cref="Limit"/> 1–30, default 10.</summary>
public sealed class GetSimilarVersesRequest
{
    public int BibleBookId { get; set; }
    public int Chapter { get; set; }
    public int? Limit { get; set; }
}

/// <summary><c>GET api/elberfelder/similarVerses/verse/{hebRefId}</c>: up to 30 hits of one verse (for "more").</summary>
public sealed class GetSimilarVersesOfVerseRequest
{
    public int HebRefId { get; set; }
}

/// <summary>Similar verses of a whole chapter: one entry per Elberfelder verse of the chapter, with an empty <c>hits</c> list when there are none.</summary>
public sealed class GetSimilarVersesResponse
{
    public int BibleBookId { get; set; }
    public int Chapter { get; set; }
    public List<VerseSimilarVersesResponse> Verses { get; set; } = [];
}

/// <summary>The hits of one source verse, best first.</summary>
public sealed class VerseSimilarVersesResponse
{
    public int HebRefId { get; set; }
    public int BibleBookId { get; set; }
    public int Chapter { get; set; }
    public int Verse { get; set; }
    public List<SimilarVerseHitResponse> Hits { get; set; } = [];
}

public sealed class SimilarVerseHitResponse
{
    /// <summary>Target verse, Elberfelder numbering (the reader's key): <c>bookId * 1_000_000 + chapter * 1_000 + verse</c>.</summary>
    public int HebRefId { get; set; }

    public int BibleBookId { get; set; }
    public int Chapter { get; set; }
    public int Verse { get; set; }

    /// <summary>Elb1871 text of the target verse.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Similarity score; can exceed 1 because of the phrase bonus. Only meaningful relative to the other hits of the verse.</summary>
    public double Score { get; set; }

    /// <summary>1-based position within the source verse.</summary>
    public int Rank { get; set; }

    /// <summary>Distinct content Strong's shared by both verses (the stored count).</summary>
    public int SharedCount { get; set; }

    /// <summary>Matched bigrams (the stored count).</summary>
    public int PhraseCount { get; set; }

    /// <summary>Shared content Strong's, rarest first.</summary>
    public List<SimilarSharedStrongsResponse> Shared { get; set; } = [];

    /// <summary>Matched runs of at least two Strong's in the source verse's order.</summary>
    public List<SimilarPhraseResponse> Phrases { get; set; } = [];
}

public sealed class SimilarSharedStrongsResponse
{
    /// <summary>As stored by STEP: <c>G1366</c>, <c>H0430G</c>.</summary>
    public string Strongs { get; set; } = string.Empty;

    /// <summary>Original word (Greek or Hebrew); empty when the lexicon has none.</summary>
    public string Lemma { get; set; } = string.Empty;

    public string Transliteration { get; set; } = string.Empty;
    public string Gloss { get; set; } = string.Empty;
}

public sealed class SimilarPhraseResponse
{
    /// <summary>The lemmas of <c>strongs</c> joined by a space, e.g. "ῥομφαία δίστομος ὀξεῖα".</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>The Strong's of the run in order.</summary>
    public List<string> Strongs { get; set; } = [];
}
