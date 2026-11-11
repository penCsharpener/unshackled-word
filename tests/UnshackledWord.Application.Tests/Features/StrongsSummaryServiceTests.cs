using UnshackledWord.Application.Features.StrongsStats;
using UnshackledWord.Application.Repositories;
using UnshackledWord.Domain.Models.Study;

namespace UnshackledWord.Application.Tests.Features;

public class StrongsSummaryServiceTests
{
    private readonly IStrongsLexiconRepository _lexicon = Substitute.For<IStrongsLexiconRepository>();
    private readonly IStrongsStatsRepository _stats = Substitute.For<IStrongsStatsRepository>();
    private readonly StrongsSummaryService _sut;

    public StrongsSummaryServiceTests()
    {
        _sut = new StrongsSummaryService(_lexicon, _stats);
    }

    private void GivenEntries(params LexiconEntry[] entries)
    {
        _lexicon.GetEntriesAsync(Arg.Any<StrongsNumber>(), Arg.Any<CancellationToken>()).Returns(entries);
    }

    private void GivenBooks(params StrongsBookCountRow[] rows)
    {
        _stats.GetBookCountsAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(rows);
    }

    private static LexiconEntry Entry(int number, string? extra, string word = "λέγω", string gloss = "say", int languageId = 2)
    {
        return new LexiconEntry { LanguageId = languageId, Number = number, Extra = extra, OriginalWord = word, Transliteration = "legō", Gloss = gloss };
    }

    [Fact]
    public async Task Totals_AreSummedFromByBook_AndUnmappedIsTheRest()
    {
        GivenEntries(Entry(1577, null, "ἐκκλησία", "assembly"));
        GivenBooks(
            new StrongsBookCountRow { BibleBookId = 44, Count = 23, VerseCount = 22 },
            new StrongsBookCountRow { BibleBookId = 45, Count = 7, VerseCount = 7 });
        _stats.GetMappedCountAsync(Arg.Any<IReadOnlyList<string>>(), false, Arg.Any<CancellationToken>()).Returns(28);
        _stats.GetRenderingsAsync(Arg.Any<IReadOnlyList<string>>(), false, Arg.Any<CancellationToken>())
            .Returns([new StrongsRenderingRow { Word = "Versammlung", Key = "versammlung", Count = 28 }]);

        var result = await _sut.GetAsync("g1577");

        result.Status.ShouldBe(StrongsSummaryStatus.Ok);
        var r = result.Response!;
        r.Strongs.ShouldBe("G1577");
        r.Language.ShouldBe("greek");
        r.Lemma.ShouldBe("ἐκκλησία");
        r.Gloss.ShouldBe("assembly");
        r.OccurrenceCount.ShouldBe(30);
        r.VerseCount.ShouldBe(29);
        r.MappedCount.ShouldBe(28);
        r.UnmappedCount.ShouldBe(2);
        r.ByBook.Select(b => b.BibleBookId).ShouldBe([44, 45]);
        r.Renderings.Single().Key.ShouldBe("versammlung");
        r.Variants.ShouldBeNull();
    }

    [Fact]
    public async Task SingleKey_DoesNotQueryKeyCounts()
    {
        GivenEntries(Entry(1577, null));
        GivenBooks(new StrongsBookCountRow { BibleBookId = 44, Count = 1, VerseCount = 1 });

        await _sut.GetAsync("G1577");

        await _stats.DidNotReceive().GetKeyCountsAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SeveralKeys_ListVariantsIncludingThoseWithoutHits_AndJoinGlosses()
    {
        GivenEntries(Entry(3004, "G", "λέγω", "say"), Entry(3004, "H", "λέγω", "tell"), Entry(3004, "I", "λέγω", "say"));
        GivenBooks(new StrongsBookCountRow { BibleBookId = 40, Count = 10, VerseCount = 9 });
        _stats.GetKeyCountsAsync(Arg.Any<IReadOnlyList<string>>(), false, Arg.Any<CancellationToken>())
            .Returns([new StrongsKeyCountRow { Strongs = "G3004G", Count = 6 }, new StrongsKeyCountRow { Strongs = "G3004H", Count = 4 }]);

        var r = (await _sut.GetAsync("G3004")).Response!;

        r.Gloss.ShouldBe("say; tell");
        r.Variants!.Select(v => (v.Strongs, v.Count)).ShouldBe([("G3004", 0), ("G3004G", 6), ("G3004H", 4), ("G3004I", 0)]);
        r.Variants[1].Lemma.ShouldBe("λέγω");
        r.Variants[0].Lemma.ShouldBe(string.Empty);
    }

    [Theory]
    [InlineData(6, 10, "aramaic")]
    [InlineData(5, 10, "hebrew")]
    [InlineData(0, 10, "hebrew")]
    public async Task Hebrew_IsAramaicOnlyWhenTheMajorityOfWordsIs(int aramaic, int count, string expected)
    {
        GivenEntries(Entry(426, null, "אֱלָהּ", "God", languageId: 0));
        GivenBooks(new StrongsBookCountRow { BibleBookId = 15, Count = count, VerseCount = count, AramaicCount = aramaic });

        (await _sut.GetAsync("H0426")).Response!.Language.ShouldBe(expected);
    }

    [Theory]
    [InlineData(1, "aramaic")]
    [InlineData(0, "hebrew")]
    public async Task Hebrew_WithoutOccurrences_FallsBackToTheLexiconLanguage(int languageId, string expected)
    {
        GivenEntries(Entry(9999, null, "x", "y", languageId));
        GivenBooks();

        var r = (await _sut.GetAsync("H9999")).Response!;

        r.Language.ShouldBe(expected);
        r.OccurrenceCount.ShouldBe(0);
        r.Renderings.ShouldBeEmpty();
        r.ByBook.ShouldBeEmpty();
    }

    [Fact]
    public async Task NoEntriesAndNoOccurrences_IsNotFound()
    {
        GivenEntries();
        GivenBooks();

        (await _sut.GetAsync("G9999")).Status.ShouldBe(StrongsSummaryStatus.NotFound);
    }

    [Fact]
    public async Task OccurrencesWithoutLexiconEntry_OmitTheHeaderFields()
    {
        GivenEntries();
        GivenBooks(new StrongsBookCountRow { BibleBookId = 40, Count = 2, VerseCount = 2 });

        var r = (await _sut.GetAsync("G9998")).Response!;

        r.Lemma.ShouldBeNull();
        r.Gloss.ShouldBeNull();
        r.UnmappedCount.ShouldBe(2);
    }

    [Theory]
    [InlineData("")]
    [InlineData("26")]
    [InlineData("X12")]
    public async Task InvalidStrongs_IsInvalid(string strongs)
    {
        (await _sut.GetAsync(strongs)).Status.ShouldBe(StrongsSummaryStatus.Invalid);
    }
}
