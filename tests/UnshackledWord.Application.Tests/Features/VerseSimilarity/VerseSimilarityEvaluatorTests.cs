using UnshackledWord.Application.Features.VerseSimilarity;

namespace UnshackledWord.Application.Tests.Features.VerseSimilarity;

/// <summary>Roadmap 0027.6: recall against raw TSK links and golden pair ranks on a toy ranking.</summary>
public class VerseSimilarityEvaluatorTests
{
    // OT verses Gen 1:1.., NT verses Mt 1:1..
    private const int Gen1 = 1_001_001;
    private const int Mt1 = 40_001_001;

    private static VerseSimilarityHit H(int source, int target, int rank) => new(source, target, rank, 1.0 / rank, 1, 0);

    /// <summary>Gen 1:1 has 40 hits: Gen 2:1 … Gen 2:40 at ranks 1..40 (TopK above 30 to test the cut).</summary>
    private static IEnumerable<VerseSimilarityHit> GenHits() => Enumerable.Range(1, 40).Select(r => H(Gen1, 1_002_000 + r, r));

    private static VerseSimilarityTestamentRecall Recall(VerseSimilarityEvaluation e, string testament) => e.Testaments.Single(t => t.Testament == testament);

    [Fact]
    public void Recall_CountsLinksAtRank10And30_PerTestament()
    {
        var links = new[]
        {
            new TskLink(Gen1, 1_002_005, null), // rank 5: @10 and @30
            new TskLink(Gen1, 1_002_025, null), // rank 25: @30 only
            new TskLink(Gen1, 1_002_035, null), // rank 35: stored but beyond 30
            new TskLink(Gen1, 1_003_001, null), // not a hit
            new TskLink(Mt1, 40_001_002, null), // NT, no hits for Mt 1:1
        };

        var e = VerseSimilarityEvaluator.Evaluate(GenHits(), links, topK: 40);

        Recall(e, "old").ShouldBe(new VerseSimilarityTestamentRecall("old", 4, 1, 2, 0.25, 0.5));
        Recall(e, "new").ShouldBe(new VerseSimilarityTestamentRecall("new", 1, 0, 0, 0, 0));
        e.TopK.ShouldBe(40);
        e.Error.ShouldBeNull();
    }

    [Fact]
    public void CrossTestamentLinks_SelfLinks_AndDuplicates_AreIgnored()
    {
        var links = new[]
        {
            new TskLink(Gen1, 1_002_005, null),
            new TskLink(Gen1, 1_002_005, null), // duplicate (multi-mapped copy)
            new TskLink(Gen1, 40_001_001, null), // OT → NT
            new TskLink(Mt1, Gen1, null), // NT → OT
            new TskLink(Gen1, Gen1, null), // self
            new TskLink(Gen1, 1_001_001, 1_001_003), // range containing the source
        };

        var e = VerseSimilarityEvaluator.Evaluate(GenHits(), links, topK: 30);

        Recall(e, "old").Links.ShouldBe(1);
        Recall(e, "old").HitsAt30.ShouldBe(1);
        Recall(e, "new").Links.ShouldBe(0);
        Recall(e, "new").RecallAt30.ShouldBe(0, "no links is 0, not NaN");
    }

    [Fact]
    public void RangeTarget_CountsWhenAnyVerseOfTheRangeIsAHit_WithItsBestRank()
    {
        var links = new[]
        {
            new TskLink(Gen1, 1_002_028, 1_002_032), // best rank 28
            new TskLink(Gen1, 1_002_012, 1_002_008), // backward range 8..12: best rank 8
            new TskLink(Gen1, 1_002_031, 1_002_039), // only ranks 31..39
        };

        var e = VerseSimilarityEvaluator.Evaluate(GenHits(), links, topK: 40);

        Recall(e, "old").ShouldBe(new VerseSimilarityTestamentRecall("old", 3, 1, 2, 0.3333, 0.6667));
    }

    [Fact]
    public void GoldenPairs_GetRanksInBothDirections_NullWhenMissing()
    {
        var golden = new[] { new VerseSimilarityGoldenPair(58_004_012, 66_002_012), new VerseSimilarityGoldenPair(19_149_006, 23_041_015) };
        var hits = new[] { H(58_004_012, 66_001_016, 1), H(58_004_012, 66_002_012, 11), H(66_002_012, 58_004_012, 29) };

        var e = VerseSimilarityEvaluator.Evaluate(hits, [], topK: 30, golden);

        e.GoldenPairs.ShouldBe([
            new VerseSimilarityGoldenRank("Hebr 4,12", 58_004_012, "Offb 2,12", 66_002_012, 11, 29),
            new VerseSimilarityGoldenRank("Ps 149,6", 19_149_006, "Jes 41,15", 23_041_015, null, null),
        ]);
    }

    [Fact]
    public void DefaultGoldenPairs_AreTheFiveOfTheRoadmap()
    {
        var e = VerseSimilarityEvaluator.Evaluate([], [], topK: 30);

        e.GoldenPairs.Select(g => $"{g.Source} → {g.Target}").ShouldBe([
            "Hebr 4,12 → Offb 1,16", "Hebr 4,12 → Offb 2,12", "Mk 14,58 → 2Kor 5,1", "Apg 7,48 → Apg 17,24", "Ps 149,6 → Jes 41,15",
        ]);
        e.GoldenPairs.ShouldAllBe(g => g.Rank == null && g.ReverseRank == null);
    }

    [Fact]
    public void Json_RoundTrips_AndAnUnreadableColumnIsNull()
    {
        var e = VerseSimilarityEvaluator.Evaluate(GenHits(), [new TskLink(Gen1, 1_002_005, null)], topK: 30);

        var json = e.ToJson();
        json.ShouldContain("\"recallAt30\":1");
        json.ShouldContain("\"goldenPairs\":[");
        var back = VerseSimilarityEvaluation.FromJson(json).ShouldNotBeNull();
        back.Testaments.ShouldBe(e.Testaments);
        back.GoldenPairs.ShouldBe(e.GoldenPairs);

        VerseSimilarityEvaluation.FromJson(null).ShouldBeNull();
        VerseSimilarityEvaluation.FromJson("{not json").ShouldBeNull();
        VerseSimilarityEvaluation.Failed(30, "boom").ToJson().ShouldContain("\"error\":\"boom\"");
    }
}
