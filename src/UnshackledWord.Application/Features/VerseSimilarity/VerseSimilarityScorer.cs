namespace UnshackledWord.Application.Features.VerseSimilarity;

/// <summary>
/// Pure verse-similarity scorer (roadmap 0027, 03-algorithm.md). Per testament: document frequency, stop list (df share or Hebrew prefix
/// H9xxx), idf = ln(N / df), cosine over idf-weighted sets plus a bigram bonus over the stop-word-free sequences, top-K per source verse
/// above a minimum score. Candidates come from the inverted index (Strong's → verses), never from all pairs.
/// <para>
/// Weights: a verse is the vector w(s) = idf(s) over its distinct content Strong's, so
/// <c>base = Σ idf(s)² (s ∈ A∩B) / (‖A‖·‖B‖)</c> (1 for identical sets) and each shared bigram x→y adds
/// <c>PhraseWeight · (idf(x)² + idf(y)²)</c> to the numerator.
/// </para>
/// </summary>
public static class VerseSimilarityScorer
{
    /// <summary>Hebrew prefix numbers (H9001–H9xxx: article, conjunction, prepositions) are always stop words.</summary>
    public static bool IsPrefixStop(string strongs) =>
        strongs.Length >= 5 && strongs[0] == 'H' && strongs[1] == '9' && char.IsAsciiDigit(strongs[2]) && char.IsAsciiDigit(strongs[3]) && char.IsAsciiDigit(strongs[4]);

    public static VerseSimilarityCorpus BuildCorpus(IReadOnlyList<VerseTokenSequence> verses, VerseSimilarityParameters parameters)
    {
        var verseCount = verses.Count;
        var tokenIds = new Dictionary<string, int>(StringComparer.Ordinal);
        var names = new List<string>();
        var raw = new int[verseCount][];
        var verseIds = new int[verseCount];
        long rawTokenCount = 0;

        for (var v = 0; v < verseCount; v++)
        {
            var strongs = verses[v].Strongs;
            var ids = new int[strongs.Count];
            for (var k = 0; k < strongs.Count; k++)
            {
                if (!tokenIds.TryGetValue(strongs[k], out var id))
                {
                    id = names.Count;
                    tokenIds.Add(strongs[k], id);
                    names.Add(strongs[k]);
                }

                ids[k] = id;
            }

            raw[v] = ids;
            verseIds[v] = verses[v].VerseId;
            rawTokenCount += ids.Length;
        }

        var tokenCount = names.Count;
        var df = new int[tokenCount];
        var seen = new int[tokenCount];
        Array.Fill(seen, -1);
        for (var v = 0; v < verseCount; v++)
        {
            foreach (var id in raw[v])
            {
                if (seen[id] != v)
                {
                    seen[id] = v;
                    df[id]++;
                }
            }
        }

        var threshold = parameters.StopDfShare * verseCount;
        var isStop = new bool[tokenCount];
        var idf = new double[tokenCount];
        for (var t = 0; t < tokenCount; t++)
        {
            isStop[t] = df[t] > threshold || IsPrefixStop(names[t]);
            idf[t] = isStop[t] ? 0 : Math.Log((double)verseCount / df[t]);
        }

        var sequences = new int[verseCount][];
        var sets = new int[verseCount][];
        var bigrams = new long[verseCount][];
        var norms = new double[verseCount];
        var postingCounts = new int[tokenCount];

        for (var v = 0; v < verseCount; v++)
        {
            var sequence = raw[v].Where(id => !isStop[id]).ToArray();
            var set = sequence.Distinct().ToArray();
            Array.Sort(set);

            var pairs = new long[Math.Max(0, sequence.Length - 1)];
            for (var k = 0; k + 1 < sequence.Length; k++)
            {
                pairs[k] = VerseSimilarityCorpus.BigramKey(sequence[k], sequence[k + 1]);
            }

            var distinctPairs = pairs.Distinct().ToArray();
            Array.Sort(distinctPairs);

            double normSquared = 0;
            foreach (var id in set)
            {
                normSquared += idf[id] * idf[id];
                postingCounts[id]++;
            }

            sequences[v] = sequence;
            sets[v] = set;
            bigrams[v] = distinctPairs;
            norms[v] = Math.Sqrt(normSquared);
        }

        var postings = new int[tokenCount][];
        for (var t = 0; t < tokenCount; t++)
        {
            postings[t] = postingCounts[t] == 0 ? [] : new int[postingCounts[t]];
        }

        var fill = new int[tokenCount];
        for (var v = 0; v < verseCount; v++)
        {
            foreach (var id in sets[v])
            {
                postings[id][fill[id]++] = v;
            }
        }

        return new VerseSimilarityCorpus(parameters, verseIds, names.ToArray(), df, isStop, idf, sequences, sets, bigrams, norms, postings, rawTokenCount);
    }

    /// <summary>Top-K hits of one verse (empty when the verse is unknown or has no content tokens).</summary>
    public static IReadOnlyList<VerseSimilarityHit> ScoreVerse(VerseSimilarityCorpus corpus, int verseId)
    {
        if (!corpus.TryGetIndex(verseId, out var index))
        {
            return [];
        }

        return new Scratch(corpus.VerseCount).Score(corpus, index);
    }

    /// <summary>Top-K hits of every verse, ordered by source verse then rank. Runs in parallel; the result is deterministic.</summary>
    public static IReadOnlyList<VerseSimilarityHit> ScoreAll(VerseSimilarityCorpus corpus, CancellationToken token = default)
    {
        var perVerse = new VerseSimilarityHit[corpus.VerseCount][];
        Parallel.For(
            0,
            corpus.VerseCount,
            new ParallelOptions { CancellationToken = token },
            () => new Scratch(corpus.VerseCount),
            (index, _, scratch) =>
            {
                perVerse[index] = scratch.Score(corpus, index);
                return scratch;
            },
            _ => { });

        var hits = new List<VerseSimilarityHit>(perVerse.Sum(h => h.Length));
        foreach (var verseHits in perVerse)
        {
            hits.AddRange(verseHits);
        }

        return hits;
    }

    /// <summary>The shared Strong's (rarest first) and matched phrases of one pair; null when either verse is unknown.</summary>
    public static VerseSimilarityExplanation? Explain(VerseSimilarityCorpus corpus, int sourceVerseId, int targetVerseId)
    {
        if (!corpus.TryGetIndex(sourceVerseId, out var a) || !corpus.TryGetIndex(targetVerseId, out var b))
        {
            return null;
        }

        var shared = corpus.Sets[a].Intersect(corpus.Sets[b])
            .Select(t => new SharedStrongs(corpus.TokenNames[t], corpus.Idf[t]))
            .OrderByDescending(s => s.Idf).ThenBy(s => s.Strongs, StringComparer.Ordinal)
            .ToList();

        var sharedBigrams = corpus.Bigrams[a].Intersect(corpus.Bigrams[b]).ToHashSet();
        var phrases = new List<IReadOnlyList<string>>();
        var seenPhrases = new HashSet<string>(StringComparer.Ordinal);
        var sequence = corpus.Sequences[a];
        var k = 0;
        while (k + 1 < sequence.Length)
        {
            if (!sharedBigrams.Contains(VerseSimilarityCorpus.BigramKey(sequence[k], sequence[k + 1])))
            {
                k++;
                continue;
            }

            var run = new List<string> { corpus.TokenNames[sequence[k]] };
            while (k + 1 < sequence.Length && sharedBigrams.Contains(VerseSimilarityCorpus.BigramKey(sequence[k], sequence[k + 1])))
            {
                run.Add(corpus.TokenNames[sequence[k + 1]]);
                k++;
            }

            if (seenPhrases.Add(string.Join(' ', run)))
            {
                phrases.Add(run);
            }
        }

        var (score, _, _) = PairScore(corpus, a, b, shared.Sum(s => s.Idf * s.Idf), shared.Count);

        return new VerseSimilarityExplanation(sourceVerseId, targetVerseId, score, shared, phrases);
    }

    private static (double Score, int SharedCount, int PhraseCount) PairScore(VerseSimilarityCorpus corpus, int a, int b, double sharedWeight, int sharedCount)
    {
        var denominator = corpus.Norms[a] * corpus.Norms[b];
        if (denominator <= 0)
        {
            return (0, sharedCount, 0);
        }

        var phraseCount = 0;
        double phraseWeight = 0;

        // A shared bigram needs two shared tokens; skip the merge otherwise.
        if (sharedCount >= 2)
        {
            var left = corpus.Bigrams[a];
            var right = corpus.Bigrams[b];
            int i = 0, j = 0;
            while (i < left.Length && j < right.Length)
            {
                if (left[i] < right[j])
                {
                    i++;
                }
                else if (left[i] > right[j])
                {
                    j++;
                }
                else
                {
                    var (x, y) = VerseSimilarityCorpus.SplitBigram(left[i]);
                    phraseWeight += corpus.Idf[x] * corpus.Idf[x] + corpus.Idf[y] * corpus.Idf[y];
                    phraseCount++;
                    i++;
                    j++;
                }
            }
        }

        var score = (sharedWeight + corpus.Parameters.PhraseWeight * phraseWeight) / denominator;
        return (score, sharedCount, phraseCount);
    }

    /// <summary>Per-thread accumulators over all verses, reset after each source verse.</summary>
    private sealed class Scratch
    {
        private readonly double[] _weight;
        private readonly int[] _count;
        private readonly List<int> _touched = [];
        private readonly List<VerseSimilarityHit> _candidates = [];

        public Scratch(int verseCount)
        {
            _weight = new double[verseCount];
            _count = new int[verseCount];
        }

        public VerseSimilarityHit[] Score(VerseSimilarityCorpus corpus, int a)
        {
            var parameters = corpus.Parameters;
            if (corpus.Norms[a] <= 0 || parameters.TopK <= 0)
            {
                return [];
            }

            foreach (var t in corpus.Sets[a])
            {
                var weight = corpus.Idf[t] * corpus.Idf[t];
                foreach (var b in corpus.Postings[t])
                {
                    if (b == a)
                    {
                        continue;
                    }

                    if (_count[b] == 0)
                    {
                        _touched.Add(b);
                    }

                    _count[b]++;
                    _weight[b] += weight;
                }
            }

            var source = corpus.VerseIds[a];
            foreach (var b in _touched)
            {
                var (score, shared, phrases) = PairScore(corpus, a, b, _weight[b], _count[b]);
                if (score >= parameters.MinScore)
                {
                    _candidates.Add(new VerseSimilarityHit(source, corpus.VerseIds[b], 0, score, shared, phrases));
                }

                _weight[b] = 0;
                _count[b] = 0;
            }

            _touched.Clear();

            _candidates.Sort(static (x, y) =>
            {
                var byScore = y.Score.CompareTo(x.Score);
                return byScore != 0 ? byScore : x.TargetVerseId.CompareTo(y.TargetVerseId);
            });

            var take = Math.Min(parameters.TopK, _candidates.Count);
            var result = new VerseSimilarityHit[take];
            for (var r = 0; r < take; r++)
            {
                result[r] = _candidates[r] with { Rank = r + 1 };
            }

            _candidates.Clear();
            return result;
        }
    }
}
