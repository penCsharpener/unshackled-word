using Microsoft.Extensions.Logging.Abstractions;
using UnshackledWord.Application.Features.VerseSimilarity;
using UnshackledWord.Domain.Models.Dbo.VerseSimilarity;

namespace UnshackledWord.Application.Tests.Features.VerseSimilarity;

/// <summary>Roadmap 0027.3: mapping, ordering and dropping of <see cref="VerseSimilarityReadService"/>; the explanation comes from the corpus of the stored run.</summary>
public class VerseSimilarityReadServiceTests
{
    private const int Source = 58004012;
    private const int RevTarget = 66002012;
    private const int OtherTarget = 40001001;
    private const int NoElbTarget = 40001002;

    private readonly IVerseSimilarityRepository _repository = Substitute.For<IVerseSimilarityRepository>();
    private readonly IVerseTokenRepository _tokens = Substitute.For<IVerseTokenRepository>();
    private readonly VerseSimilarityExplainerCache _cache = new();

    private VerseSimilarityReadService NewService() => new(_repository, _tokens, _cache, NullLogger<VerseSimilarityReadService>.Instance);

    private static SimilarHitRow Hit(int source, int target, int? targetHeb, int rank, int runId = 7) => new()
    {
        SourceHebRefId = source, SourceLxxRefId = source, TargetLxxRefId = target, TargetHebRefId = targetHeb, TargetText = targetHeb is null ? null : $"text {targetHeb}",
        Rank = rank, Score = 0.5m + rank, SharedCount = 2, PhraseCount = 1, RunId = runId,
    };

    private void GivenCorpus()
    {
        _repository.GetRunAsync(7, Arg.Any<CancellationToken>()).Returns(new VerseSimilarityRunDbo { Id = 7, StopDfShare = 1, TopK = 30, MinScore = 0, PhraseWeight = 0.5m });

        // Greek: A rare, B rarer, C common. The source has A B C; the target C B (order differs) and A; the third target has only C.
        _tokens.LoadAsync(Testament.New, Arg.Any<CancellationToken>()).Returns(new List<VerseTokenSequence>
        {
            new(Source, ["G0001", "G0002", "G0003"]),
            new(RevTarget, ["G0003", "G0002", "G0001"]),
            new(OtherTarget, ["G0003"]),
            new(NoElbTarget, ["G0003", "G0009"]),
            new(40001003, ["G0003"]),
            new(40001004, ["G0003"]),
            new(40001005, ["G0002", "G0005"]),
        });
        _tokens.GetLabelsAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>()).Returns(ci =>
            (IReadOnlyDictionary<string, StrongsLabel>)ci.Arg<IReadOnlyCollection<string>>()
                .Where(s => s != "G0003")
                .ToDictionary(s => s, s => new StrongsLabel(s, $"lemma{s}", $"gloss{s}", $"tr{s}")));
    }

    [Fact]
    public async Task Chapter_ReturnsEveryVerse_WithEmptyHitsWhenThereAreNone()
    {
        _repository.GetPageAsync(58004000, 58004999, 10, Arg.Any<CancellationToken>()).Returns(new SimilarVersePage(
            [new SimilarSourceVerse { HebRefId = 58004011, LxxRefId = 58004011 }, new SimilarSourceVerse { HebRefId = 58004012, LxxRefId = 58004012 }], []));

        var response = await NewService().GetChapterAsync(58, 4, 10, TestContext.Current.CancellationToken);

        response.BibleBookId.ShouldBe(58);
        response.Chapter.ShouldBe(4);
        response.Verses.Select(v => v.Verse).ShouldBe([11, 12]);
        response.Verses.ShouldAllBe(v => v.Hits.Count == 0);
        await _tokens.DidNotReceive().LoadAsync(Arg.Any<Testament>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Hits_AreResolved_ExplainedRarestFirst_AndUnresolvedOnesDropped()
    {
        GivenCorpus();
        _repository.GetPageAsync(Source, Source, 30, Arg.Any<CancellationToken>()).Returns(new SimilarVersePage(
            [new SimilarSourceVerse { HebRefId = Source, LxxRefId = Source }],
            [
                Hit(Source, RevTarget, 66002012, 1),
                Hit(Source, NoElbTarget, null, 2),
                Hit(Source, OtherTarget, 40001001, 3),
                Hit(Source, 66002013, 66002012, 4), // the same Elberfelder verse again
                Hit(Source, 99, Source, 5), // the verse itself
            ]));

        var verse = await NewService().GetVerseAsync(Source, TestContext.Current.CancellationToken);

        verse.ShouldNotBeNull();
        verse.Hits.Select(h => (h.HebRefId, h.Rank)).ShouldBe([(66002012, 1), (40001001, 3)]);

        var first = verse.Hits[0];
        first.BibleBookId.ShouldBe(66);
        first.Chapter.ShouldBe(2);
        first.Verse.ShouldBe(12);
        first.Text.ShouldBe("text 66002012");
        first.Score.ShouldBe(1.5);
        first.SharedCount.ShouldBe(2);
        first.PhraseCount.ShouldBe(1);

        // idf: G0001 df 2 (source, Rev), G0002 df 3, G0003 df 6 of 7 verses: rarest first.
        first.Shared.Select(s => s.Strongs).ShouldBe(["G0001", "G0002", "G0003"]);
        first.Shared[0].Lemma.ShouldBe("lemmaG0001");
        first.Shared[0].Transliteration.ShouldBe("trG0001");
        first.Shared[0].Gloss.ShouldBe("glossG0001");
        first.Shared[2].Lemma.ShouldBe(string.Empty, "no lexicon entry: empty, not missing");

        // The target has the words in the opposite order (G0003 G0002 G0001), so no bigram of the source matches.
        first.Phrases.ShouldBeEmpty();
    }

    [Fact]
    public async Task Phrase_IsTheMatchedRunInSourceOrder_WithLemmasAndFallbackToStrongs()
    {
        GivenCorpus();
        _tokens.LoadAsync(Testament.New, Arg.Any<CancellationToken>()).Returns(new List<VerseTokenSequence>
        {
            new(Source, ["G0001", "G0002", "G0003", "G0004"]),
            new(RevTarget, ["G0009", "G0001", "G0002", "G0003"]),
        });
        _repository.GetPageAsync(Source, Source, 30, Arg.Any<CancellationToken>()).Returns(new SimilarVersePage(
            [new SimilarSourceVerse { HebRefId = Source, LxxRefId = Source }], [Hit(Source, RevTarget, 66002012, 1)]));

        var hit = (await NewService().GetVerseAsync(Source, TestContext.Current.CancellationToken))!.Hits.Single();

        var phrase = hit.Phrases.ShouldHaveSingleItem();
        phrase.Strongs.ShouldBe(["G0001", "G0002", "G0003"]);
        phrase.Text.ShouldBe("lemmaG0001 lemmaG0002 G0003", "a Strong's without a lemma shows as its number");
    }

    [Fact]
    public async Task Cache_BuildsOncePerRun_AndRebuildsWhenTheRunChanges()
    {
        GivenCorpus();
        _repository.GetPageAsync(Source, Source, 30, Arg.Any<CancellationToken>()).Returns(new SimilarVersePage(
            [new SimilarSourceVerse { HebRefId = Source, LxxRefId = Source }], [Hit(Source, RevTarget, 66002012, 1)]));
        var service = NewService();

        await service.GetVerseAsync(Source, TestContext.Current.CancellationToken);
        await service.GetVerseAsync(Source, TestContext.Current.CancellationToken);
        await _tokens.Received(1).LoadAsync(Testament.New, Arg.Any<CancellationToken>());
        _cache.CachedRunId.ShouldBe(7);

        _repository.GetRunAsync(8, Arg.Any<CancellationToken>()).Returns(new VerseSimilarityRunDbo { Id = 8, StopDfShare = 1, TopK = 30, MinScore = 0, PhraseWeight = 0.5m });
        _repository.GetPageAsync(Source, Source, 30, Arg.Any<CancellationToken>()).Returns(new SimilarVersePage(
            [new SimilarSourceVerse { HebRefId = Source, LxxRefId = Source }], [Hit(Source, RevTarget, 66002012, 1, runId: 8)]));

        await service.GetVerseAsync(Source, TestContext.Current.CancellationToken);
        await _tokens.Received(2).LoadAsync(Testament.New, Arg.Any<CancellationToken>());
        _cache.CachedRunId.ShouldBe(8);
    }

    [Fact]
    public async Task Verse_IsNull_WhenTheElberfelderVerseDoesNotExist()
    {
        _repository.GetPageAsync(1001099, 1001099, 30, Arg.Any<CancellationToken>()).Returns(new SimilarVersePage([], []));

        (await NewService().GetVerseAsync(1001099, TestContext.Current.CancellationToken)).ShouldBeNull();
    }

    [Theory]
    [InlineData(1001001, Testament.Old)]
    [InlineData(39004006, Testament.Old)]
    [InlineData(40001001, Testament.New)]
    public void TestamentOf_SplitsAfterMalachi(int refId, Testament expected) => SimilarVerseMapper.TestamentOf(refId).ShouldBe(expected);
}
