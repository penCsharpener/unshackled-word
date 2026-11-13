using UnshackledWord.Application.Features.StrongsStats;
using UnshackledWord.Application.Repositories;
using UnshackledWord.Domain.Models.Study;
using UnshackledWord.Domain.WebApi.Elberfelder.GetStrongsOccurrences;
using UnshackledWord.Domain.WebApi.Elberfelder.GetStrongsSummary;
using UnshackledWord.Domain.WebApi.Elberfelder.GetWordStats;

namespace UnshackledWord.Application.Tests.Features;

public class WordStatsServiceTests
{
    private readonly IStrongsStatsRepository _stats = Substitute.For<IStrongsStatsRepository>();
    private readonly WordStatsService _sut;

    public WordStatsServiceTests()
    {
        _sut = new WordStatsService(_stats);
    }

    [Theory]
    [InlineData("all", 1, 66)]
    [InlineData("OT", 1, 39)]
    [InlineData("nt", 40, 66)]
    public async Task Testament_MapsToABookRange(string testament, int first, int last)
    {
        _stats.GetWordBookCountsAsync("versammlung", first, last, Arg.Any<CancellationToken>())
            .Returns([new WordBookCountRow { BibleBookId = 1, Count = 1, VerseCount = 1, Word = "Versammlung" }]);

        var result = await _sut.GetAsync("Versammlung", testament);

        result!.Testament.ShouldBe(testament.ToLowerInvariant());
        await _stats.Received(1).GetWordOriginalsAsync("versammlung", first, last, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvalidTestament_ReturnsNull()
    {
        (await _sut.GetAsync("Gott", "xx")).ShouldBeNull();
    }

    [Fact]
    public async Task UnknownWord_ReturnsZerosAndTheRequestAsGiven()
    {
        var result = await _sut.GetAsync("Quatsch", "all");

        result!.Word.ShouldBe("Quatsch");
        result.OccurrenceCount.ShouldBe(0);
        result.ByBook.ShouldBeEmpty();
        result.Originals.ShouldBeEmpty();
        await _stats.DidNotReceive().GetWordOriginalsAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Word_ComesFromTheBookWithTheHighestCount_AndTotalsAreSummed()
    {
        _stats.GetWordBookCountsAsync("gott", 1, 66, Arg.Any<CancellationToken>()).Returns(
        [
            new WordBookCountRow { BibleBookId = 1, Count = 5, VerseCount = 4, UnmappedCount = 1, Word = "gott" },
            new WordBookCountRow { BibleBookId = 43, Count = 9, VerseCount = 8, UnmappedCount = 2, Word = "Gott" }
        ]);

        var result = await _sut.GetAsync("gott", "all");

        result!.Word.ShouldBe("Gott");
        result.OccurrenceCount.ShouldBe(14);
        result.VerseCount.ShouldBe(12);
        result.UnmappedCount.ShouldBe(3);
        result.ByBook.Select(b => b.BibleBookId).ShouldBe([1, 43]);
    }

    [Fact]
    public async Task Originals_MapTheLanguage()
    {
        _stats.GetWordBookCountsAsync("gott", 1, 66, Arg.Any<CancellationToken>())
            .Returns([new WordBookCountRow { BibleBookId = 1, Count = 5, VerseCount = 4, Word = "Gott" }]);
        _stats.GetWordOriginalsAsync("gott", 1, 66, Arg.Any<CancellationToken>()).Returns(
        [
            new WordOriginalRow { Strongs = "G2316", Count = 3, TotalCount = 1300, Lemma = "θεός" },
            new WordOriginalRow { Strongs = "H0426", Count = 2, TotalCount = 95, LexiconLanguageId = 1 },
            new WordOriginalRow { Strongs = "H0430G", Count = 1, TotalCount = 2000, LexiconLanguageId = 0 },
            new WordOriginalRow { Strongs = "H9999", Count = 1, TotalCount = 1 }
        ]);

        var originals = (await _sut.GetAsync("gott", "all"))!.Originals;

        originals.Select(o => o.Language).ShouldBe(["greek", "aramaic", "hebrew", "hebrew"]);
        originals[0].Count.ShouldBe(3);
        originals[0].TotalCount.ShouldBe(1300);
        originals[0].Lemma.ShouldBe("θεός");
    }

    private static readonly DateTime StatsAt = new(2026, 11, 13, 8, 0, 0, DateTimeKind.Utc);

    private static SnapshotLemmaRow Lemma(string key, string? pos, string word, int occurrences, string books, int unmapped = 0)
    {
        return new SnapshotLemmaRow { Key = key, Pos = pos, Word = word, Occurrences = occurrences, LemmaOccurrences = occurrences, UnmappedCount = unmapped, BookCounts = books };
    }

    private void GivenResolved(string word, params SnapshotLemmaRow[] rows)
    {
        _stats.GetSnapshotRefreshedAtAsync(Arg.Any<CancellationToken>()).Returns(StatsAt);
        _stats.ResolveSnapshotLemmasAsync(word, Arg.Any<CancellationToken>()).Returns(rows);
        _stats.GetSnapshotWordOriginalsAsync(Arg.Any<IReadOnlyList<SnapshotLemmaKey>>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns([new WordOriginalRow { Strongs = "H1980", Count = 7, TotalCount = 20, Lemma = "הלך" }]);
    }

    [Fact]
    public async Task Snapshot_FormHit_ReportsTheResolvedLemma_AndSumsTheBooks()
    {
        GivenResolved("ging", Lemma("gehen", "VERB", "gehen", 12, "[{\"b\":1,\"c\":5,\"v\":4,\"u\":1},{\"b\":40,\"c\":7,\"v\":6,\"u\":0}]", unmapped: 1));

        var r = (await _sut.GetAsync("Ging", "all"))!;

        r.Lemma.ShouldBe("gehen");
        r.Pos.ShouldBe("VERB");
        r.Word.ShouldBe("gehen");
        r.StatsRefreshedAt.ShouldBe(StatsAt);
        r.OccurrenceCount.ShouldBe(12);
        r.VerseCount.ShouldBe(10);
        r.UnmappedCount.ShouldBe(1);
        r.ByBook.Select(b => b.BibleBookId).ShouldBe([1, 40]);
        r.Originals.Single().Strongs.ShouldBe("H1980");
        r.Originals.Single().Language.ShouldBe("hebrew");
        await _stats.DidNotReceive().GetWordBookCountsAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Snapshot_Homograph_SumsPosByDefault_AndOffersBothAsCandidates()
    {
        GivenResolved("gang", Lemma("gang", "NOUN", "Gang", 30, "[{\"b\":2,\"c\":30,\"v\":20}]"), Lemma("gang", "VERB", "gang", 2, "[{\"b\":2,\"c\":2,\"v\":2}]"));

        var r = (await _sut.GetAsync("gang", "all"))!;

        r.OccurrenceCount.ShouldBe(32);
        r.Pos.ShouldBeNull();
        r.Candidates!.Select(c => (c.Lemma, c.Pos, c.Occurrences)).ShouldBe([("Gang", "NOUN", 30), ("gang", "VERB", 2)]);
    }

    [Fact]
    public async Task Snapshot_FormWithTwoLemmas_AnswersWithTheMostFrequent_AndListsBothCandidates()
    {
        GivenResolved("ging", Lemma("gehen", "VERB", "gehen", 1513, "[{\"b\":1,\"c\":1513,\"v\":1000}]"), Lemma("ging", "VERB", "ging", 1, "[{\"b\":1,\"c\":1,\"v\":1}]"));

        var r = (await _sut.GetAsync("ging", "all"))!;

        r.Lemma.ShouldBe("gehen");
        r.OccurrenceCount.ShouldBe(1513);
        r.Candidates!.Select(c => c.Lemma).ShouldBe(["gehen", "ging"]);
    }

    [Fact]
    public async Task Snapshot_PosFilter_KeepsOnlyThatPos_CaseInsensitively_ButAllCandidates()
    {
        GivenResolved("gang", Lemma("gang", "NOUN", "Gang", 30, "[{\"b\":2,\"c\":30,\"v\":20}]"), Lemma("gang", "VERB", "gang", 2, "[{\"b\":2,\"c\":2,\"v\":2}]"));

        var r = (await _sut.GetAsync("gang", "all", "verb"))!;

        r.OccurrenceCount.ShouldBe(2);
        r.Pos.ShouldBe("VERB");
        r.Candidates!.Count.ShouldBe(2);
        await _stats.Received(1).GetSnapshotWordOriginalsAsync(Arg.Is<IReadOnlyList<SnapshotLemmaKey>>(k => k.Count == 1 && k[0].Pos == "VERB"), null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Snapshot_PosFilterWithoutMatch_GivesZerosButStillTheCandidates()
    {
        GivenResolved("gang", Lemma("gang", "NOUN", "Gang", 30, "[{\"b\":2,\"c\":30,\"v\":20}]"));

        var r = (await _sut.GetAsync("gang", "all", "ADJ"))!;

        r.OccurrenceCount.ShouldBe(0);
        r.Candidates!.Count.ShouldBe(1);
        r.Originals.ShouldBeEmpty();
    }

    [Fact]
    public async Task Snapshot_UnknownWord_GivesZeros_AndTheRequestAsGiven()
    {
        GivenResolved("quatsch");

        var r = (await _sut.GetAsync("Quatsch", "nt"))!;

        r.Word.ShouldBe("Quatsch");
        r.Lemma.ShouldBeNull();
        r.StatsRefreshedAt.ShouldBe(StatsAt);
        r.OccurrenceCount.ShouldBe(0);
        await _stats.DidNotReceive().GetSnapshotWordOriginalsAsync(Arg.Any<IReadOnlyList<SnapshotLemmaKey>>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("ot", "H", 1, 39)]
    [InlineData("nt", "G", 40, 66)]
    [InlineData("all", null, 1, 66)]
    public async Task Snapshot_Testament_LimitsBooksAndLanguage(string testament, string? lang, int first, int last)
    {
        GivenResolved("gehen", Lemma("gehen", "VERB", "gehen", 12, "[{\"b\":1,\"c\":5,\"v\":4},{\"b\":40,\"c\":7,\"v\":6}]"));

        var r = (await _sut.GetAsync("gehen", testament))!;

        r.ByBook.ShouldAllBe(b => b.BibleBookId >= first && b.BibleBookId <= last);
        await _stats.Received(1).GetSnapshotWordOriginalsAsync(Arg.Any<IReadOnlyList<SnapshotLemmaKey>>(), lang, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NeverRefreshed_FallsBackToTheLivePath()
    {
        _stats.GetWordBookCountsAsync("gott", 1, 66, Arg.Any<CancellationToken>())
            .Returns([new WordBookCountRow { BibleBookId = 1, Count = 5, VerseCount = 4, Word = "Gott" }]);

        var r = (await _sut.GetAsync("Gott", "all"))!;

        r.OccurrenceCount.ShouldBe(5);
        r.StatsRefreshedAt.ShouldBeNull();
        await _stats.DidNotReceive().ResolveSnapshotLemmasAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}

public class StrongsStatsValidatorTests
{
    [Theory]
    [InlineData("Versammlung", "ot", true)]
    [InlineData("Gott's-Wort", "ALL", true)]
    [InlineData("Gott", "nt", true)]
    [InlineData("", "all", false)]
    [InlineData("zwei Worte", "all", false)]
    [InlineData("Gott1", "all", false)]
    [InlineData("Gott", "both", false)]
    public void WordStats_ChecksWordAndTestament(string word, string testament, bool valid)
    {
        new GetWordStatsRequestValidator().Validate(new GetWordStatsRequest { Word = word, Testament = testament }).IsValid.ShouldBe(valid);
    }

    [Fact]
    public void WordStats_RejectsTooLongWords()
    {
        new GetWordStatsRequestValidator().Validate(new GetWordStatsRequest { Word = new string('a', 51) }).IsValid.ShouldBeFalse();
        new GetWordStatsRequestValidator().Validate(new GetWordStatsRequest { Word = new string('a', 50) }).IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("G1577", true)]
    [InlineData("h0430g", true)]
    [InlineData("1577", false)]
    [InlineData("", false)]
    public void Summary_ChecksTheStrongsFormat(string strongs, bool valid)
    {
        new GetStrongsSummaryRequestValidator().Validate(new GetStrongsSummaryRequest { Strongs = strongs }).IsValid.ShouldBe(valid);
    }

    [Theory]
    [InlineData(null, null, true)]
    [InlineData(1, "versammlung", true)]
    [InlineData(66, null, true)]
    [InlineData(0, null, false)]
    [InlineData(67, null, false)]
    [InlineData(null, "", false)]
    public void Occurrences_ChecksBookAndRendering(int? book, string? rendering, bool valid)
    {
        new GetStrongsOccurrencesRequestValidator()
            .Validate(new GetStrongsOccurrencesRequest { Strongs = "G1577", Book = book, Rendering = rendering }).IsValid.ShouldBe(valid);
    }

    [Fact]
    public void Occurrences_RejectsTooLongRenderingAndBadPaging()
    {
        var validator = new GetStrongsOccurrencesRequestValidator();
        validator.Validate(new GetStrongsOccurrencesRequest { Strongs = "G1577", Rendering = new string('a', 51) }).IsValid.ShouldBeFalse();
        validator.Validate(new GetStrongsOccurrencesRequest { Strongs = "G1577", Page = 0 }).IsValid.ShouldBeFalse();
    }
}
