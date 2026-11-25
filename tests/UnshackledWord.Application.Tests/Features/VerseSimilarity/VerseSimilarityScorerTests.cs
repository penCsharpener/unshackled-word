using UnshackledWord.Application.Features.VerseSimilarity;

namespace UnshackledWord.Application.Tests.Features.VerseSimilarity;

public class VerseSimilarityScorerTests
{
    private static VerseTokenSequence V(int id, string tokens) => new(id, tokens.Split(' ', StringSplitOptions.RemoveEmptyEntries));

    private static VerseSimilarityCorpus Corpus(VerseSimilarityParameters parameters, params VerseTokenSequence[] verses) =>
        VerseSimilarityScorer.BuildCorpus(verses, parameters);

    [Fact]
    public void FrequentStrongs_IsAStopWord_AndNeverMatches()
    {
        // S is in 4 of 5 verses (> 0.5 · 5): cut. A is in 2: content.
        var corpus = Corpus(new VerseSimilarityParameters(StopDfShare: 0.5, MinScore: 0),
            V(1, "S A X1"), V(2, "S A X2"), V(3, "S X3"), V(4, "S X4"), V(5, "X5"));

        corpus.StopList().Select(s => s.Strongs).ShouldBe(["S"]);
        VerseSimilarityScorer.Explain(corpus, 1, 2)!.Shared.Select(s => s.Strongs).ShouldBe(["A"]);
        VerseSimilarityScorer.ScoreVerse(corpus, 3).ShouldBeEmpty("3 and 4 share only the stop word");
    }

    [Fact]
    public void HebrewPrefix_IsAlwaysAStopWord()
    {
        var corpus = Corpus(new VerseSimilarityParameters(StopDfShare: 1, MinScore: 0),
            V(1, "H9003 H0001"), V(2, "H9003 H0002"), V(3, "H0003"));

        VerseSimilarityScorer.IsPrefixStop("H9003").ShouldBeTrue();
        VerseSimilarityScorer.IsPrefixStop("H0430G").ShouldBeFalse();
        VerseSimilarityScorer.IsPrefixStop("G9003").ShouldBeFalse();
        corpus.StopList().Select(s => s.Strongs).ShouldBe(["H9003"]);
        VerseSimilarityScorer.ScoreVerse(corpus, 1).ShouldBeEmpty();
    }

    [Fact]
    public void ArticleBetweenTwoContentWords_KeepsTheBigram()
    {
        // ART is a stop word (4 of 6 verses); "A ART B" and "A B" share the bigram A→B.
        var corpus = Corpus(new VerseSimilarityParameters(StopDfShare: 0.5, MinScore: 0),
            V(1, "A ART B C"), V(2, "A B D"), V(3, "ART E"), V(4, "ART F"), V(5, "B A H"), V(6, "ART G"));

        var hits = VerseSimilarityScorer.ScoreVerse(corpus, 1);
        var toTwo = hits.Single(h => h.TargetVerseId == 2);
        var toFive = hits.Single(h => h.TargetVerseId == 5);

        toTwo.SharedCount.ShouldBe(2);
        toTwo.PhraseCount.ShouldBe(1);
        toFive.PhraseCount.ShouldBe(0, "B A is the other order");
        toTwo.Score.ShouldBeGreaterThan(toFive.Score);
        VerseSimilarityScorer.Explain(corpus, 1, 2)!.Phrases.ShouldHaveSingleItem().ShouldBe(["A", "B"]);
    }

    [Fact]
    public void Cosine_PrefersTheShortExactVerse_OverALongListVerse()
    {
        var corpus = Corpus(new VerseSimilarityParameters(StopDfShare: 1, MinScore: 0, PhraseWeight: 0),
            V(1, "A B"), V(2, "A B"), V(3, "A B C D E F G H"),
            V(4, "C X1"), V(5, "D X2"), V(6, "E X3"), V(7, "F X4"), V(8, "G X5"), V(9, "H X6"), V(10, "X7"));

        var hits = VerseSimilarityScorer.ScoreVerse(corpus, 1);

        hits.Select(h => h.TargetVerseId).Take(2).ShouldBe([2, 3]);
        hits[0].Score.ShouldBe(1, 1e-9, "identical sets have cosine 1");
        hits[1].Score.ShouldBeLessThan(0.7);
    }

    [Fact]
    public void Self_IsNeverAHit()
    {
        var corpus = Corpus(new VerseSimilarityParameters(StopDfShare: 1, MinScore: 0),
            V(1, "A B"), V(2, "A B"), V(3, "A C"), V(4, "D"));

        var hits = VerseSimilarityScorer.ScoreAll(corpus);

        hits.ShouldNotBeEmpty();
        hits.ShouldAllBe(h => h.SourceVerseId != h.TargetVerseId);
        hits.Where(h => h.SourceVerseId == 1).Select(h => h.TargetVerseId).ShouldBe([2, 3]);
    }

    [Fact]
    public void TopKAndMinScore_AreRespected()
    {
        // Verse 1 shares A with 2..6; the longer the target, the lower the score.
        var verses = new List<VerseTokenSequence>
        {
            V(1, "A"), V(2, "A"), V(3, "A P1"), V(4, "A P1 P2"), V(5, "A P1 P2 P3"), V(6, "A P1 P2 P3 P4 P5 P6"),
        };
        verses.AddRange(Enumerable.Range(10, 14).Select(i => V(i, $"Z{i}")));

        var all = VerseSimilarityScorer.ScoreVerse(Corpus(new VerseSimilarityParameters(StopDfShare: 1, TopK: 30, MinScore: 0), verses.ToArray()), 1);
        all.Select(h => h.TargetVerseId).ShouldBe([2, 3, 4, 5, 6]);

        var topThree = VerseSimilarityScorer.ScoreVerse(Corpus(new VerseSimilarityParameters(StopDfShare: 1, TopK: 3, MinScore: 0), verses.ToArray()), 1);
        topThree.Select(h => h.Rank).ShouldBe([1, 2, 3]);
        topThree.Select(h => h.TargetVerseId).ShouldBe([2, 3, 4]);
        topThree.Select(h => h.Score).ShouldBeInOrder(SortDirection.Descending);

        var minScore = all[2].Score;
        var aboveMin = VerseSimilarityScorer.ScoreVerse(Corpus(new VerseSimilarityParameters(StopDfShare: 1, TopK: 30, MinScore: minScore), verses.ToArray()), 1);
        aboveMin.Select(h => h.TargetVerseId).ShouldBe([2, 3, 4]);
        aboveMin.ShouldAllBe(h => h.Score >= minScore);
    }
}
