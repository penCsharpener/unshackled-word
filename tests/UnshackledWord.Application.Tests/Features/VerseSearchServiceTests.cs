using UnshackledWord.Application.Features.VerseSearch;
using UnshackledWord.Application.Repositories;
using UnshackledWord.Domain.Models.Study;

namespace UnshackledWord.Application.Tests.Features;

public class VerseSearchServiceTests
{
    private readonly IVerseSearchRepository _verses = Substitute.For<IVerseSearchRepository>();
    private readonly IStrongsStatsRepository _stats = Substitute.For<IStrongsStatsRepository>();
    private readonly VerseSearchService _sut;

    public VerseSearchServiceTests()
    {
        _sut = new VerseSearchService(_verses, _stats);
        _stats.GetSnapshotRefreshedAtAsync(Arg.Any<CancellationToken>()).Returns(new DateTime(2026, 11, 13, 8, 0, 0, DateTimeKind.Utc));
        _stats.ResolveSnapshotLemmasAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(new List<SnapshotLemmaRow>());
        _verses.GetLexemesAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(c => c.Arg<IReadOnlyList<string>>().Select(w => new VerseSearchLexemeRow { Word = w, Lexeme = w }).ToList());
        _verses.GetBookCountsAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([new VerseSearchBookCountRow { BookId = 1, Count = 5 }, new VerseSearchBookCountRow { BookId = 43, Count = 17 }]);
        _verses.GetHitsAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([new VerseSearchHitRow { HebRefId = 43003016 }]);
        _verses.GetWordsAsync(Arg.Any<IReadOnlyList<int>>(), Arg.Any<CancellationToken>()).Returns(
        [
            new VerseSearchWordRow { HebRefId = 43003016, PositionInVerse = 1, WordInContext = "Denn", Stems = ["denn"], Lemma = "denn" },
            new VerseSearchWordRow { HebRefId = 43003016, PositionInVerse = 2, WordInContext = "Gott", Stems = ["gott"], Lemma = "gott" },
            new VerseSearchWordRow { HebRefId = 43003016, PositionInVerse = 3, WordInContext = "ging", Stems = ["ging"], Lemma = "gehen" }
        ]);
    }

    private void GivenLemma(string word, params string[] lemmas)
    {
        _stats.ResolveSnapshotLemmasAsync(word, Arg.Any<CancellationToken>())
            .Returns(lemmas.Select(l => new SnapshotLemmaRow { Key = l, Word = l, Occurrences = 10, LemmaOccurrences = 10 }).ToList());
    }

    [Fact]
    public async Task NoPositiveWord_ReturnsNull()
    {
        (await _sut.SearchAsync("-Gott", 1, 20, null, 100)).ShouldBeNull();
    }

    [Fact]
    public async Task BookCounts_AreIndependentOfBookIdAndPaging_AndTotalFollowsTheFilter()
    {
        var all = (await _sut.SearchAsync("Gott", 1, 20, null, 100))!;
        var book = (await _sut.SearchAsync("Gott", 1, 20, 43, 100))!;
        var page2 = (await _sut.SearchAsync("Gott", 2, 10, null, 100))!;

        all.Total.ShouldBe(22);
        all.BookCounts.Sum(c => c.Count).ShouldBe(all.Total);
        book.Total.ShouldBe(17);
        book.BookCounts.Select(c => (c.BookId, c.Count)).ShouldBe(all.BookCounts.Select(c => (c.BookId, c.Count)));
        page2.BookCounts.Count.ShouldBe(2);
        await _verses.Received(1).GetHitsAsync(Arg.Any<string>(), Arg.Is<string?>(x => x == null), 100, 43_000_000, 43_999_999, 0, 20, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PageBeyondTheTotal_ReturnsNoHits_ButStillTheTotalAndBookCounts()
    {
        _verses.GetHitsAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), 40, Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns([]);

        var response = (await _sut.SearchAsync("Gott", 3, 20, null, 100))!;

        response.Hits.ShouldBeEmpty();
        response.Total.ShouldBe(22);
        response.BookCounts.Count.ShouldBe(2);
        await _verses.DidNotReceive().GetWordsAsync(Arg.Any<IReadOnlyList<int>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LiteralQuery_IsOnlyPassedWhenTheQueryWasWidenedByLemmas()
    {
        GivenLemma("ging", "gehen");

        await _sut.SearchAsync("ging", 1, 20, null, 100);
        await _sut.SearchAsync("Gott", 1, 20, null, 100);

        await _verses.Received(1).GetHitsAsync("(('ging' | 'gehen':B))", "(('ging'))", 100, 0, int.MaxValue, 0, 20, Arg.Any<CancellationToken>());
        await _verses.Received(1).GetHitsAsync("(('gott'))", null, 100, 0, int.MaxValue, 0, 20, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Hits_CarryTheReferenceAndMarkStemAndLemmaMatches()
    {
        GivenLemma("ging", "gehen");

        var response = (await _sut.SearchAsync("Gott ging", 1, 20, null, 100))!;

        var hit = response.Hits.Single();
        (hit.BibleBookId, hit.Chapter, hit.Verse).ShouldBe((43, 3, 16));
        hit.Tokens.Select(t => (t.Text, t.Matched)).ShouldBe([("Denn", false), ("Gott", true), ("ging", true)]);
        response.Tokens.Single(t => t.Text == "ging").Lemmas.ShouldBe(["gehen"]);
        response.Tokens.Single(t => t.Text == "gott").Lemmas.ShouldBeNull();
    }

    [Fact]
    public async Task RevisedHit_ShowsTheVariantWords_FlaggedWithItsLevel_WhileBaseHitsStayUnflagged()
    {
        _verses.GetHitsAsync(Arg.Any<string>(), Arg.Any<string?>(), 200, Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([new VerseSearchHitRow { HebRefId = 43003016, RevisionVerseId = 7, Level = 200 }, new VerseSearchHitRow { HebRefId = 43003017 }]);
        _verses.GetRevisionWordsAsync(Arg.Is<IReadOnlyList<int>>(ids => ids.SequenceEqual(new[] { 7 })), Arg.Any<CancellationToken>())
            .Returns([new VerseSearchWordRow { HebRefId = 43003016, PositionInVerse = 1, WordInContext = "einzigen", Stems = ["einzig"] }]);
        _verses.GetWordsAsync(Arg.Is<IReadOnlyList<int>>(ids => ids.SequenceEqual(new[] { 43003017 })), Arg.Any<CancellationToken>())
            .Returns([new VerseSearchWordRow { HebRefId = 43003017, PositionInVerse = 1, WordInContext = "eingeborenen", Stems = ["eingebor"] }]);
        _verses.GetLexemesAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns([new VerseSearchLexemeRow { Word = "einzigen", Lexeme = "einzig" }]);

        var response = (await _sut.SearchAsync("einzigen", 1, 20, null, 200))!;

        var revised = response.Hits[0];
        revised.Revised.ShouldBe(true);
        revised.Level.ShouldBe(200);
        revised.Tokens.Select(t => (t.Text, t.Matched)).ShouldBe([("einzigen", true)]);
        response.Hits[1].Revised.ShouldBeNull();
        response.Hits[1].Level.ShouldBeNull();
        response.Hits[1].Tokens.Single().Text.ShouldBe("eingeborenen");
    }

    [Fact]
    public async Task BaseLevelSearch_NeverReadsRevisionWords()
    {
        await _sut.SearchAsync("Gott", 1, 20, null, 100);

        await _verses.DidNotReceive().GetRevisionWordsAsync(Arg.Any<IReadOnlyList<int>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LemmaMatch_WithoutStemMatch_IsMarked()
    {
        GivenLemma("gehen", "gehen");
        _verses.GetLexemesAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns([new VerseSearchLexemeRow { Word = "gehen", Lexeme = "geh" }]);

        var response = (await _sut.SearchAsync("gehen", 1, 20, null, 100))!;

        response.Hits.Single().Tokens.Single(t => t.Text == "ging").Matched.ShouldBeTrue();
    }

    [Fact]
    public async Task UnpopulatedSnapshot_IsStemOnly()
    {
        _stats.GetSnapshotRefreshedAtAsync(Arg.Any<CancellationToken>()).Returns((DateTime?)null);

        await _sut.SearchAsync("ging", 1, 20, null, 100);

        await _stats.DidNotReceive().ResolveSnapshotLemmasAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _verses.Received().GetBookCountsAsync("(('ging'))", 100, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LemmaWidening_IsLimitedToTheThreeMostFrequentLemmas()
    {
        GivenLemma("sein", "sein", "seinen", "seine", "seines");
        _verses.GetLexemesAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns([new VerseSearchLexemeRow { Word = "sein", Lexeme = "sein" }]);

        var response = (await _sut.SearchAsync("sein", 1, 20, null, 100))!;

        // "sein" equals its own stem and is not repeated as a lemma.
        response.Tokens.Single().Lemmas.ShouldBe(["seinen", "seine"]);
    }

    [Fact]
    public async Task Phrases_AndExclusions_AreNotLemmaWidened()
    {
        GivenLemma("ging", "gehen");

        await _sut.SearchAsync("\"er ging\" -kam", 1, 20, null, 100);

        await _stats.DidNotReceive().ResolveSnapshotLemmasAsync("ging", Arg.Any<CancellationToken>());
        await _stats.DidNotReceive().ResolveSnapshotLemmasAsync("kam", Arg.Any<CancellationToken>());
    }
}
