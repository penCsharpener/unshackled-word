using UnshackledWord.Application.Features.VerseSearch;
using UnshackledWord.Application.Repositories;
using UnshackledWord.Domain.Models.Study;
using UnshackledWord.Domain.WebApi.Elberfelder.SearchVerses;

namespace UnshackledWord.Application.Tests.Features;

/// <summary>Roadmap 0029.10: marking positions of a verse query in one chapter and the neighbour chapters.</summary>
public class VerseSearchChapterTests
{
    private readonly IVerseSearchRepository _verses = Substitute.For<IVerseSearchRepository>();
    private readonly IStrongsStatsRepository _stats = Substitute.For<IStrongsStatsRepository>();
    private readonly VerseSearchService _sut;

    public VerseSearchChapterTests()
    {
        _sut = new VerseSearchService(_verses, _stats);
        _stats.GetSnapshotRefreshedAtAsync(Arg.Any<CancellationToken>()).Returns((DateTime?)null);
        _verses.GetLexemesAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(c => c.Arg<IReadOnlyList<string>>().Select(w => new VerseSearchLexemeRow { Word = w, Lexeme = w }).ToList());
    }

    private static VerseSearchWordRow Word(int hebRefId, int position, string text, string? lemma = null)
    {
        return new VerseSearchWordRow { HebRefId = hebRefId, PositionInVerse = position, WordInContext = text, Stems = [text.ToLowerInvariant()], Lemma = lemma };
    }

    [Fact]
    public async Task Chapter_ReturnsThePositionsOfTheMatchedWords_OfTheWholeQueryHitsOnly()
    {
        _verses.GetHitsAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([new VerseSearchHitRow { HebRefId = 43003016 }]);
        _verses.GetWordsAsync(Arg.Any<IReadOnlyList<int>>(), Arg.Any<CancellationToken>()).Returns(
        [
            Word(43003016, 1, "Denn"), Word(43003016, 2, "Gott"), Word(43003016, 3, "hat"), Word(43003016, 4, "die"),
            Word(43003016, 5, "Welt"), Word(43003016, 6, "geliebt")
        ]);

        var response = (await _sut.ChapterAsync("Gott Welt geliebt", 43, 3, 200))!;

        var verse = response.Verses.Single();
        verse.Verse.ShouldBe(16);
        verse.Revised.ShouldBeNull();
        verse.Positions.ShouldBe([2, 5, 6]);
        response.Total.ShouldBe(3);
        await _verses.Received(1).GetHitsAsync("(('gott') & ('welt') & ('geliebt'))", Arg.Is<string?>(x => x == null), Arg.Is<int>(x => x == 200), Arg.Is<int>(x => x == 43_003_000), Arg.Is<int>(x => x == 43_003_999), Arg.Is<int>(x => x == 0), Arg.Is<int>(x => x >= 176), Arg.Any<CancellationToken>());
        await _verses.DidNotReceive().GetRevisionWordsAsync(Arg.Any<IReadOnlyList<int>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Chapter_FlagsVariantPositions_AndOrdersByVerse()
    {
        _verses.GetHitsAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([new VerseSearchHitRow { HebRefId = 43003017 }, new VerseSearchHitRow { HebRefId = 43003016, RevisionVerseId = 7, Level = 200 }]);
        _verses.GetRevisionWordsAsync(Arg.Any<IReadOnlyList<int>>(), Arg.Any<CancellationToken>())
            .Returns([Word(43003016, 1, "Gott"), Word(43003016, 2, "liebt")]);
        _verses.GetWordsAsync(Arg.Any<IReadOnlyList<int>>(), Arg.Any<CancellationToken>())
            .Returns([Word(43003017, 1, "Gott"), Word(43003017, 2, "sandte")]);

        var response = (await _sut.ChapterAsync("Gott", 43, 3, 200))!;

        response.Verses.Select(v => (v.Verse, v.Revised, string.Join(",", v.Positions))).ShouldBe([(16, true, "1"), (17, null, "1")]);
        response.Total.ShouldBe(2);
    }

    [Fact]
    public async Task Chapter_MatchesByLemma_AndSkipsVersesWithoutAMatchedWord()
    {
        _stats.GetSnapshotRefreshedAtAsync(Arg.Any<CancellationToken>()).Returns(new DateTime(2026, 11, 13, 8, 0, 0, DateTimeKind.Utc));
        _stats.ResolveSnapshotLemmasAsync("gehen", Arg.Any<CancellationToken>())
            .Returns([new SnapshotLemmaRow { Key = "gehen", Word = "gehen", Occurrences = 10, LemmaOccurrences = 10 }]);
        _verses.GetLexemesAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns([new VerseSearchLexemeRow { Word = "gehen", Lexeme = "geh" }]);
        _verses.GetHitsAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([new VerseSearchHitRow { HebRefId = 43003001 }, new VerseSearchHitRow { HebRefId = 43003002 }]);
        _verses.GetWordsAsync(Arg.Any<IReadOnlyList<int>>(), Arg.Any<CancellationToken>())
            .Returns([Word(43003001, 1, "ging", "gehen"), Word(43003002, 1, "kam", "kommen")]);

        var response = (await _sut.ChapterAsync("gehen", 43, 3, 200))!;

        response.Verses.Single().Positions.ShouldBe([1]);
    }

    [Fact]
    public async Task Chapter_WithoutAPositiveWord_IsNull()
    {
        (await _sut.ChapterAsync("-Gott", 43, 3, 200)).ShouldBeNull();
        (await _sut.NeighboursAsync("-Gott", 43, 3, 200)).ShouldBeNull();
    }

    [Fact]
    public async Task Neighbours_AreTheNearestHitChaptersOnEitherSide_ForAChapterWithOrWithoutHit()
    {
        _verses.GetHitChaptersAsync(Arg.Any<string>(), 200, Arg.Any<CancellationToken>()).Returns([1001, 43001, 43003, 43005, 66022]);

        var inside = (await _sut.NeighboursAsync("Gott", 43, 3, 200))!;
        var between = (await _sut.NeighboursAsync("Gott", 43, 4, 200))!;
        var first = (await _sut.NeighboursAsync("Gott", 1, 1, 200))!;
        var last = (await _sut.NeighboursAsync("Gott", 66, 22, 200))!;

        (inside.Previous!.BibleBookId, inside.Previous.Chapter, inside.Next!.BibleBookId, inside.Next.Chapter).ShouldBe((43, 1, 43, 5));
        (between.Previous!.Chapter, between.Next!.Chapter).ShouldBe((3, 5));
        first.Previous.ShouldBeNull();
        first.Next!.BibleBookId.ShouldBe(43);
        last.Next.ShouldBeNull();
        last.Previous!.Chapter.ShouldBe(5);
    }

    [Theory]
    [InlineData("Gott", 43, 3, 200, true)]
    [InlineData("Gott", 0, 3, 200, false)]
    [InlineData("Gott", 67, 3, 200, false)]
    [InlineData("Gott", 43, 0, 200, false)]
    [InlineData("Gott", 43, 3, 1001, false)]
    [InlineData("", 43, 3, 200, false)]
    [InlineData("-Gott", 43, 3, 200, false)]
    [InlineData("a b c d e f g h i", 43, 3, 200, false)]
    public void Validators_ApplyTheVerseSearchLimits(string q, int book, int chapter, int level, bool valid)
    {
        new SearchVersesChapterRequestValidator().Validate(new SearchVersesChapterRequest { Q = q, BibleBookId = book, Chapter = chapter, Level = level }).IsValid.ShouldBe(valid);
        new SearchVersesNeighboursRequestValidator().Validate(new SearchVersesNeighboursRequest { Q = q, BibleBookId = book, Chapter = chapter, Level = level }).IsValid.ShouldBe(valid);
    }
}
