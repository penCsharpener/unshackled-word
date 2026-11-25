namespace UnshackledWord.Application.Features.VerseSimilarity;

/// <summary>
/// One testament prepared for scoring (roadmap 0027, 03-algorithm.md): Strong's interned to int ids, document frequency, stop list,
/// idf, and per verse the stop-word-free sequence, the distinct content set, the distinct content bigrams and the vector norm.
/// The inverted index (Strong's → verses) holds content tokens only. Built by <see cref="VerseSimilarityScorer.BuildCorpus"/>.
/// </summary>
public sealed class VerseSimilarityCorpus
{
    private readonly Dictionary<int, int> _indexByVerseId;

    internal VerseSimilarityCorpus(
        VerseSimilarityParameters parameters,
        int[] verseIds,
        string[] tokenNames,
        int[] documentFrequency,
        bool[] isStop,
        double[] idf,
        int[][] sequences,
        int[][] sets,
        long[][] bigrams,
        double[] norms,
        int[][] postings,
        long rawTokenCount)
    {
        Parameters = parameters;
        VerseIds = verseIds;
        TokenNames = tokenNames;
        DocumentFrequency = documentFrequency;
        IsStop = isStop;
        Idf = idf;
        Sequences = sequences;
        Sets = sets;
        Bigrams = bigrams;
        Norms = norms;
        Postings = postings;
        RawTokenCount = rawTokenCount;

        _indexByVerseId = new Dictionary<int, int>(verseIds.Length);
        for (var i = 0; i < verseIds.Length; i++)
        {
            _indexByVerseId[verseIds[i]] = i;
        }
    }

    public VerseSimilarityParameters Parameters { get; }

    /// <summary>Verses in the corpus (N for df and idf).</summary>
    public int VerseCount => VerseIds.Length;

    /// <summary>All Strong's tokens as loaded, stop words included.</summary>
    public long RawTokenCount { get; }

    /// <summary>Tokens left after the stop list (repeats within a verse counted).</summary>
    public long ContentTokenCount => Sequences.Sum(s => (long)s.Length);

    /// <summary>Distinct Strong's in the testament.</summary>
    public int DistinctStrongsCount => TokenNames.Length;

    /// <summary>The cut list: Strong's above the df threshold and Hebrew prefix numbers, most frequent first.</summary>
    public IReadOnlyList<StopStrongs> StopList() =>
        Enumerable.Range(0, TokenNames.Length)
            .Where(t => IsStop[t])
            .Select(t => new StopStrongs(TokenNames[t], DocumentFrequency[t]))
            .OrderByDescending(s => s.DocumentFrequency).ThenBy(s => s.Strongs, StringComparer.Ordinal)
            .ToList();

    public bool Contains(int verseId) => _indexByVerseId.ContainsKey(verseId);

    internal bool TryGetIndex(int verseId, out int index) => _indexByVerseId.TryGetValue(verseId, out index);

    internal int[] VerseIds { get; }
    internal string[] TokenNames { get; }
    internal int[] DocumentFrequency { get; }
    internal bool[] IsStop { get; }
    internal double[] Idf { get; }

    /// <summary>Per verse: content token ids in word order, repeats kept.</summary>
    internal int[][] Sequences { get; }

    /// <summary>Per verse: distinct content token ids, ascending.</summary>
    internal int[][] Sets { get; }

    /// <summary>Per verse: distinct content bigrams of <see cref="Sequences"/> as <c>(x &lt;&lt; 32) | y</c>, ascending.</summary>
    internal long[][] Bigrams { get; }

    /// <summary>Per verse: sqrt(Σ idf² over the set).</summary>
    internal double[] Norms { get; }

    /// <summary>Per token id: verse indices containing it (ascending); empty for stop words.</summary>
    internal int[][] Postings { get; }

    internal static long BigramKey(int x, int y) => ((long)x << 32) | (uint)y;

    internal static (int X, int Y) SplitBigram(long key) => ((int)(key >> 32), (int)(key & 0xFFFFFFFF));
}
