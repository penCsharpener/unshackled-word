using UnshackledWord.Application.Features.GrammarSearch;
using UnshackledWord.Domain.Models.Grammar;

namespace UnshackledWord.Application.Tests.Features.GrammarSearch;

/// <summary>Roadmap 0029.6: the service builds Hebrew slot sides, rejects keys of the wrong language and reads Hebrew mappings, with a substituted repository.</summary>
public class GrammarHebrewServiceTests
{
    private const int Gen1Start = 1_001_000;
    private static readonly GrammarLemma Bara = new("H1254", "בָּרָא", "ba.ra", "to create");
    private static readonly GrammarLemma Agape = new("G0026", "ἀγάπη", "agape", "love");

    private readonly IGrammarSearchRepository _repository = Substitute.For<IGrammarSearchRepository>();
    private readonly GrammarSearchService _service;

    public GrammarHebrewServiceTests()
    {
        var words = Substitute.For<IGrammarWordReader>();
        words.ReadAsync("bara", Arg.Any<GrammarWordMode>(), Arg.Any<CancellationToken>()).Returns([Bara]);
        words.ReadAsync("G26", Arg.Any<GrammarWordMode>(), Arg.Any<CancellationToken>()).Returns([Agape]);

        var index = Substitute.For<IMorphologyCodeIndex>();
        index.GetGreekAsync(Arg.Any<CancellationToken>()).Returns(new[] { "N-NSF" }.Select(c => new MorphologyCodeEntry(c, GreekMorphologyDecoder.Decode(c))).ToList());
        index.GetHebrewAsync(Arg.Any<CancellationToken>()).Returns(new[] { "HVqp3ms", "HVNp3ms", "HTd/Ncmsa", "AVqp3ms" }.Select(c =>
        {
            var decoded = HebrewMorphologyDecoder.Decode(c);
            return new MorphologyCodeEntry(c, decoded.Root, decoded.IsAramaic);
        }).ToList());

        _repository.ReadHebrewMappingsAsync(Arg.Any<IReadOnlyList<int>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<IReadOnlyList<int>>().Select(id => new GrammarMappingRow { StepWordId = id, PositionInVerse = id % 100 }).ToList());
        _service = new GrammarSearchService(words, new GrammarFilterResolver(index), _repository);
    }

    private static GrammarMatchRow HebrewMatch(int slot, int verse, int wordId) => new() { Slot = slot, HebRefId = Gen1Start + verse, WordId = wordId, IsHebrew = true };

    [Fact]
    public async Task Chapter_HebrewLemmaWithStem_QueriesOnlyTheHebrewSideWithRootStrongsAndCodes()
    {
        IReadOnlyList<GrammarSlotQuery>? seen = null;
        _repository.ReadMatchesInRangeAsync(Arg.Do<IReadOnlyList<GrammarSlotQuery>>(q => seen = q), Gen1Start, Gen1Start + 999, Arg.Any<CancellationToken>())
            .Returns([HebrewMatch(0, 1, 102), HebrewMatch(0, 27, 1237), HebrewMatch(0, 27, 1244)]);

        var result = await _service.ChapterAsync("bara Stamm:qal", 1, 1, TestContext.Current.CancellationToken);

        var slot = seen.ShouldNotBeNull().Single();
        slot.Greek.ShouldBeNull();
        slot.Hebrew!.Codes.ShouldBe(["HVqp3ms", "AVqp3ms"]);
        slot.Hebrew.StrongsKeys!.Take(3).ShouldBe(["H1254", "H1254A", "H1254B"]);

        var response = result.Response.ShouldNotBeNull();
        response.Verses.Select(v => v.Verse).ShouldBe([1, 27]);
        response.Verses[1].OriginalWordIds.ShouldBe([1237, 1244]);
        response.Verses[1].GermanPositions.ShouldBe([37, 44]);
        response.Total.ShouldBe(3);
    }

    [Fact]
    public async Task Chapter_SlotWithoutLemma_SearchesBothLanguagesWhereTheKeyExists()
    {
        IReadOnlyList<GrammarSlotQuery>? seen = null;
        _repository.ReadMatchesInRangeAsync(Arg.Do<IReadOnlyList<GrammarSlotQuery>>(q => seen = q), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns([]);

        await _service.ChapterAsync("Wortart:substantiv", 1, 1, TestContext.Current.CancellationToken);
        var both = seen.ShouldNotBeNull().Single();
        both.Greek!.Codes.ShouldBe(["N-NSF"]);
        both.Hebrew!.Codes.ShouldBe(["HTd/Ncmsa"]);

        await _service.ChapterAsync("Stamm:qal", 1, 1, TestContext.Current.CancellationToken);
        var hebrewOnly = seen.Single();
        hebrewOnly.Greek.ShouldBeNull();
        hebrewOnly.Hebrew.ShouldNotBeNull();
    }

    [Theory]
    [InlineData("G26 Stamm:qal", "Stamm:qal")]
    [InlineData("G26 Form:qatal", "Form:qatal")]
    [InlineData("bara Tempus:aor", "Tempus:aor")]
    [InlineData("bara Kasus:nom Stamm:qal", "Kasus:nom")]
    public async Task Search_KeyThatDoesNotExistInTheLanguageOfTheLemma_Is400KeyNotInLanguage(string query, string token)
    {
        var result = await _service.SearchAsync(query, 1, null, TestContext.Current.CancellationToken);

        result.Error.ShouldNotBeNull().Code.ShouldBe(GrammarErrorCodes.KeyNotInLanguage);
        result.Error.Token.ShouldBe(token);
        result.Error.Candidates.ShouldNotBeNull().ShouldContain("Wortart");
        await _repository.DidNotReceiveWithAnyArgs().FindVersesAsync(default!, default);
    }

    [Fact]
    public async Task Facets_ForAKeyOfTheOtherLanguage_Is400KeyNotInLanguage()
    {
        var result = await _service.FacetsAsync("bara", 0, "Tempus", TestContext.Current.CancellationToken);

        result.Error.ShouldNotBeNull().Code.ShouldBe(GrammarErrorCodes.KeyNotInLanguage);
    }

    [Fact]
    public async Task Facets_OfAHebrewLemma_FoldTheHebrewCodes()
    {
        _repository.CountHebrewCodesAsync(Arg.Any<IReadOnlyList<string>?>(), Arg.Any<CancellationToken>())
            .Returns([new GrammarCodeCountRow { Code = "HVqp3ms", Count = 24 }, new GrammarCodeCountRow { Code = "HVNp3ms", Count = 10 }]);

        var result = await _service.FacetsAsync("bara", 0, "Stamm", TestContext.Current.CancellationToken);

        var facet = result.Response.ShouldNotBeNull().Facets.Single();
        facet.Key.ShouldBe("Stamm");
        facet.Values.Select(v => (v.Key, v.Count)).ShouldBe([(MorphologyKey.Qal, 24), (MorphologyKey.Niphal, 10)]);
        await _repository.DidNotReceiveWithAnyArgs().CountCodesAsync(default, default);
    }
}
