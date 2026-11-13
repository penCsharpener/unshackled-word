using UnshackledWord.Application.Features.StrongsStats;
using UnshackledWord.Application.Repositories;
using UnshackledWord.Domain.Models.Study;
using UnshackledWord.Domain.WebApi.Elberfelder.SearchOriginals;

namespace UnshackledWord.Application.Tests.Features;

public class SearchOriginalsServiceTests
{
    private static readonly DateTime StatsAt = new(2026, 11, 13, 8, 0, 0, DateTimeKind.Utc);

    private readonly IStrongsStatsRepository _stats = Substitute.For<IStrongsStatsRepository>();
    private readonly SearchOriginalsService _sut;

    public SearchOriginalsServiceTests()
    {
        _sut = new SearchOriginalsService(_stats);
        _stats.GetSnapshotRefreshedAtAsync(Arg.Any<CancellationToken>()).Returns(StatsAt);
    }

    private void GivenLemma(string token, string key, string? pos, int occurrences, int lemmaOccurrences)
    {
        _stats.ResolveSnapshotLemmasAsync(token, Arg.Any<CancellationToken>())
            .Returns([new SnapshotLemmaRow { Key = key, Pos = pos, Word = key, Occurrences = occurrences, LemmaOccurrences = lemmaOccurrences }]);
    }

    private void GivenOriginals(string key, string? pos, params (string Strongs, int Count)[] originals)
    {
        _stats.GetSnapshotLemmaOriginalsAsync(Arg.Any<IReadOnlyList<SnapshotLemmaKey>>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(originals.Select(o => new SnapshotLemmaOriginalRow { Key = key, Pos = pos, Strongs = o.Strongs, Count = o.Count, LemmaTotal = 10, TotalCount = 100 }).ToList());
    }

    [Fact]
    public async Task NeverRefreshed_ReturnsNull()
    {
        _stats.GetSnapshotRefreshedAtAsync(Arg.Any<CancellationToken>()).Returns((DateTime?)null);

        (await _sut.LookupAsync("gehen", 5, false)).ShouldBeNull();
    }

    [Theory]
    [InlineData(1999, false)]
    [InlineData(2000, true)]
    [InlineData(2001, true)]
    public async Task Threshold_IsInclusiveAt2000_AndOriginalsAreOmittedForFrequentLemmas(int lemmaOccurrences, bool frequent)
    {
        GivenLemma("sein", "sein", "AUX", lemmaOccurrences, lemmaOccurrences);
        GivenOriginals("sein", "AUX", ("G1510", 5));

        var lemma = (await _sut.LookupAsync("sein", 5, false))!.Tokens.Single().Lemmas.Single();

        lemma.HighFrequency.ShouldBe(frequent);
        lemma.Originals.Count.ShouldBe(frequent ? 0 : 1);
    }

    [Fact]
    public async Task IncludeFrequent_ListsOriginalsAnyway_AndStillFlagsTheLemma()
    {
        GivenLemma("und", "und", "CCONJ", 20000, 20000);
        GivenOriginals("und", "CCONJ", ("H9999", 5));

        var lemma = (await _sut.LookupAsync("und", 5, true))!.Tokens.Single().Lemmas.Single();

        lemma.HighFrequency.ShouldBeTrue();
        lemma.Originals.Count.ShouldBe(1);
    }

    [Fact]
    public async Task FrequencyRule_UsesTheLemmaSumOverAllPos_NotThePosRow()
    {
        GivenLemma("gang", "gang", "NOUN", 1500, 2500);

        var lemma = (await _sut.LookupAsync("gang", 5, false))!.Tokens.Single().Lemmas.Single();

        lemma.HighFrequency.ShouldBeTrue();
    }

    [Fact]
    public async Task OnlyNonFrequentLemmasAreFetched_InOneBatch()
    {
        GivenLemma("und", "und", "CCONJ", 20000, 20000);
        GivenLemma("gehen", "gehen", "VERB", 800, 800);
        GivenOriginals("gehen", "VERB", ("H1980", 4));

        await _sut.LookupAsync("und gehen", 3, false);

        await _stats.Received(1).GetSnapshotLemmaOriginalsAsync(
            Arg.Is<IReadOnlyList<SnapshotLemmaKey>>(k => k.Count == 1 && k[0].Key == "gehen"), 3, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnlyFrequentLemmas_DoNotQueryOriginalsAtAll()
    {
        GivenLemma("und", "und", "CCONJ", 20000, 20000);

        await _sut.LookupAsync("und", 5, false);

        await _stats.DidNotReceive().GetSnapshotLemmaOriginalsAsync(Arg.Any<IReadOnlyList<SnapshotLemmaKey>>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnknownToken_GivesNoLemmas_AndShareIsCountOverLemmaTotal()
    {
        GivenLemma("gehen", "gehen", "VERB", 10, 10);
        GivenOriginals("gehen", "VERB", ("G4198", 4));
        _stats.ResolveSnapshotLemmasAsync("quatsch", Arg.Any<CancellationToken>()).Returns([]);

        var r = (await _sut.LookupAsync("gehen quatsch", 5, false))!;

        r.StatsRefreshedAt.ShouldBe(StatsAt);
        r.Tokens.Select(t => t.Token).ShouldBe(["gehen", "quatsch"]);
        r.Tokens[1].Lemmas.ShouldBeEmpty();
        var original = r.Tokens[0].Lemmas.Single().Originals.Single();
        original.Share.ShouldBe(0.4);
        original.Language.ShouldBe("greek");
    }
}

public class SearchTokenizerTests
{
    [Fact]
    public void Splits_LowerCases_AndDropsDuplicates()
    {
        SearchTokenizer.Tokenize("Gott, der GOTT sprach: „Es werde Licht!“").ShouldBe(["gott", "der", "sprach", "es", "werde", "licht"]);
    }

    [Fact]
    public void KeepsInnerApostropheAndHyphen_AndNormalisesTheTypographicApostrophe()
    {
        SearchTokenizer.Tokenize("Gott-Mensch geh’n").ShouldBe(["gott-mensch", "geh'n"]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ... 123 ")]
    public void NothingUsable_GivesNoTokens(string? query)
    {
        SearchTokenizer.Tokenize(query).ShouldBeEmpty();
    }
}

public class SearchOriginalsRequestValidatorTests
{
    private readonly SearchOriginalsRequestValidator _sut = new();

    [Fact]
    public void Valid_Passes()
    {
        _sut.Validate(new SearchOriginalsRequest { Q = "gehen und stehen" }).IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("123 456")]
    public void EmptyOrWithoutWords_Fails(string q)
    {
        _sut.Validate(new SearchOriginalsRequest { Q = q }).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void TooLong_Fails()
    {
        _sut.Validate(new SearchOriginalsRequest { Q = new string('a', SearchOriginalsRequest.MaxQueryLength + 1) }).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void TooManyDistinctTokens_Fails_ButDuplicatesDoNotCount()
    {
        var many = string.Join(' ', Enumerable.Range(0, SearchOriginalsRequest.MaxTokens + 1).Select(i => "w" + new string((char)('a' + i), 2)));
        var dupes = string.Join(' ', Enumerable.Repeat("gehen", 20));

        _sut.Validate(new SearchOriginalsRequest { Q = many }).IsValid.ShouldBeFalse();
        _sut.Validate(new SearchOriginalsRequest { Q = dupes }).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void TokenLongerThan50_Fails()
    {
        _sut.Validate(new SearchOriginalsRequest { Q = new string('a', SearchOriginalsRequest.MaxTokenLength + 1) }).IsValid.ShouldBeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(21)]
    public void TopOutOfRange_Fails(int top)
    {
        _sut.Validate(new SearchOriginalsRequest { Q = "gehen", Top = top }).IsValid.ShouldBeFalse();
    }
}

public class StatsRulesTests
{
    [Theory]
    [InlineData(1999, false)]
    [InlineData(2000, true)]
    public void HighFrequencyThreshold_Boundary(int occurrences, bool expected)
    {
        StatsRules.IsHighFrequency(occurrences).ShouldBe(expected);
        StatsRules.HighFrequencyThreshold.ShouldBe(2000);
    }
}

public class SnapshotBookCountsTests
{
    [Fact]
    public void Merge_SumsPerBook_AndRestrictsTheRange()
    {
        var merged = SnapshotBookCounts.Merge(["[{\"b\":1,\"c\":2,\"v\":2,\"u\":1}]", "[{\"b\":1,\"c\":3,\"v\":2},{\"b\":40,\"c\":1,\"v\":1}]"], 1, 39);

        merged.Single().B.ShouldBe(1);
        merged.Single().C.ShouldBe(5);
        merged.Single().V.ShouldBe(4);
        merged.Single().U.ShouldBe(1);
    }

    [Fact]
    public void Parse_EmptyOrNull_IsEmpty()
    {
        SnapshotBookCounts.Parse(null).ShouldBeEmpty();
        SnapshotBookCounts.Parse("[]").ShouldBeEmpty();
    }
}
