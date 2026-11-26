using UnshackledWord.Application.Features.GrammarSearch;
using UnshackledWord.Domain.Models.Grammar;

namespace UnshackledWord.Application.Tests.Features.GrammarSearch;

/// <summary>Roadmap 0029.7: the <c>dt.</c> keys of the parser, the German word reading and the slot kinds of <see cref="GrammarSearchService"/> (substituted repository).</summary>
public class GrammarGermanSearchTests
{
    private const int Matt1Start = 40_001_000;
    private static readonly GrammarLemma Agape = new("G0026", "ἀγάπη", "agape", "love");

    private readonly IGrammarSearchRepository _repository = Substitute.For<IGrammarSearchRepository>();
    private readonly GrammarSearchService _service;

    public GrammarGermanSearchTests()
    {
        var words = Substitute.For<IGrammarWordReader>();
        words.ReadAsync("agape", Arg.Any<GrammarWordMode>(), Arg.Any<CancellationToken>()).Returns([Agape]);
        words.ReadAsync(Arg.Is<string>(w => w != "agape"), Arg.Any<GrammarWordMode>(), Arg.Any<CancellationToken>()).Returns([]);

        _repository.GermanLemmaExistsAsync("liebe", Arg.Any<CancellationToken>()).Returns(true);

        var index = Substitute.For<IMorphologyCodeIndex>();
        index.GetGreekAsync(Arg.Any<CancellationToken>()).Returns(new[] { "V-AAP-NSM", "V-AAI-3S", "N-NSF" }.Select(c => new MorphologyCodeEntry(c, GreekMorphologyDecoder.Decode(c))).ToList());
        index.GetHebrewAsync(Arg.Any<CancellationToken>()).Returns(new[] { "HVqp3ms" }.Select(c => new MorphologyCodeEntry(c, HebrewMorphologyDecoder.Decode(c).Root)).ToList());

        _repository.ReadMappingsAsync(Arg.Any<IReadOnlyList<int>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<IReadOnlyList<int>>().Select(id => new GrammarMappingRow { StepWordId = id, PositionInVerse = id % 100 }).ToList());
        _service = new GrammarSearchService(words, new GrammarFilterResolver(index), _repository);
    }

    private static GrammarParseResult Ok(string q)
    {
        var result = GrammarQueryParser.Parse(q);
        result.Error.ShouldBeNull();
        return result;
    }

    [Fact]
    public void GermanFilters_BindToTheSlotAndUseTheGermanValueSet()
    {
        var slot = Ok("Tempus:aor Modus:partizip dt.Verbform:finit dt.Tempus:präter").Slots.ShouldHaveSingleItem();

        slot.Filters.Select(f => (f.Key.Name, f.German)).ShouldBe([("Tempus", false), ("Modus", false), ("Verbform", true), ("Tempus", true)]);
        slot.Filters[2].Values.Single().DbCodes.ShouldBe(["Fin"]);
        slot.Filters[3].Values.Single().De.ShouldBe("Präteritum");
        slot.Filters[3].Values.Single().DbCodes.ShouldBe(["Past"]);
    }

    [Fact]
    public void GermanLemmaAndGermanFilter_FormOneSlot()
    {
        var slot = Ok("Liebe dt.Numerus:plural").Slots.ShouldHaveSingleItem();

        slot.Word.ShouldBe("Liebe");
        slot.Mode.ShouldBe(GrammarWordMode.Auto);
        slot.Filters.Single().Values.Single().DbCodes.ShouldBe(["P"]);
    }

    [Theory]
    [InlineData("dt.Kasus:akk")]
    [InlineData("dt.Diathese:aktiv")]
    [InlineData("Verbform:finit")]
    [InlineData("STTS:NN")]
    [InlineData("Wort:Liebe")]
    public void WithheldOrGermanOnlyKeys_AreUnknownWithoutTheirPrefix(string query)
    {
        var result = GrammarQueryParser.Parse(query);

        result.Error.ShouldNotBeNull().Code.ShouldBe(GrammarErrorCodes.UnknownKey);
    }

    [Fact]
    public void UnknownGermanKey_ListsOnlyTheGermanKeys()
    {
        var error = GrammarQueryParser.Parse("dt.Kasus:akk").Error.ShouldNotBeNull();

        error.Candidates.ShouldNotBeNull().ShouldAllBe(c => c.StartsWith("dt.", StringComparison.Ordinal));
        error.Candidates.ShouldNotContain("dt.Kasus");
        error.Candidates.ShouldContain("dt.Verbform");
    }

    [Fact]
    public void ExplicitGermanWord_ForcesTheGermanReading()
    {
        var slot = Ok("dt.Wort:Liebe dt.Wortart:Substantiv").Slots.ShouldHaveSingleItem();

        slot.Word.ShouldBe("Liebe");
        slot.Mode.ShouldBe(GrammarWordMode.German);
        slot.Filters.Single().Key.Name.ShouldBe("Wortart");
    }

    [Fact]
    public void ConjunctionCoversBothUniversalDependenciesTags_AndArtikelIsTheDeterminer()
    {
        var conj = Ok("dt.Wortart:konj").Slots[0].Filters.Single().Values.Single();
        conj.DbCodes.ShouldBe(["CCONJ", "SCONJ"]);

        Ok("dt.Wortart:Artikel").Slots[0].Filters.Single().Values.Single().DbCodes.ShouldBe(["DET"]);
    }

    [Fact]
    public void Stts_IsAFreeCodeFilter()
    {
        var filter = Ok("dt.STTS:vv*,NN").Slots[0].Filters.Single();

        filter.Key.GermanColumn.ShouldBe("Stts");
        filter.CodePatterns.ShouldBe(["VV*", "NN"]);
    }

    [Fact]
    public void GermanValueLabels_AreUnique_AndEveryGermanValueHasStoredCodes()
    {
        foreach (var key in GrammarVocabulary.Keys.Where(k => k.GermanValues.Count > 0))
        {
            key.GermanColumn.ShouldNotBeNull();
            GrammarVocabulary.GermanColumns.ShouldContain(key.GermanColumn);
            key.GermanValues.Select(v => v.Id).Distinct().Count().ShouldBe(key.GermanValues.Count, key.Name);
            key.GermanValues.ShouldAllBe(v => v.DbCodes != null && v.DbCodes.Count > 0, key.Name);
        }
    }

    [Fact]
    public async Task LatinWordThatIsNoLemma_IsReadAsGermanLemma_AndReportedAsSuch()
    {
        var result = await _service.SearchAsync("Liebe", 1, null, TestContext.Current.CancellationToken);

        var slot = result.Response!.Slots.ShouldHaveSingleItem();
        slot.ReadAs.ShouldBe("german");
        slot.GermanLemma.ShouldBe("liebe");
        slot.Lemmas.ShouldBeEmpty();
    }

    [Fact]
    public async Task WordThatIsNeitherLemmaNorGerman_StaysUnknown()
    {
        var result = await _service.SearchAsync("Xyzzyq", 1, null, TestContext.Current.CancellationToken);

        result.Error.ShouldNotBeNull().Code.ShouldBe(GrammarErrorCodes.UnknownWord);
    }

    [Fact]
    public async Task ExplicitStrongsOrLemma_NeverFallsBackToGerman()
    {
        var result = await _service.SearchAsync("Lemma:Liebe", 1, null, TestContext.Current.CancellationToken);

        result.Error.ShouldNotBeNull().Code.ShouldBe(GrammarErrorCodes.UnknownWord);
        await _repository.DidNotReceiveWithAnyArgs().GermanLemmaExistsAsync(default!, default);
    }

    [Fact]
    public async Task ExplicitGermanWord_SkipsTheOriginalReaders()
    {
        var result = await _service.SearchAsync("dt.Wort:agape", 1, null, TestContext.Current.CancellationToken);

        // "agape" is a lemma, but dt.Wort: asks for the German word, and no German lemma agape exists.
        result.Error.ShouldNotBeNull().Code.ShouldBe(GrammarErrorCodes.UnknownWord);
    }

    [Fact]
    public async Task GermanOnlySlot_HasNoOriginalSide_AndKeepsLemmaAndFilters()
    {
        IReadOnlyList<GrammarSlotQuery>? seen = null;
        _repository.FindVersesAsync(Arg.Do<IReadOnlyList<GrammarSlotQuery>>(q => seen = q), Arg.Any<CancellationToken>()).Returns([]);

        await _service.SearchAsync("Liebe dt.Numerus:plural -dt.Verbform:finit", 1, null, TestContext.Current.CancellationToken);

        var query = seen.ShouldNotBeNull().ShouldHaveSingleItem();
        query.IsGermanOnly.ShouldBeTrue();
        query.German!.Lemma.ShouldBe("liebe");
        query.German.Filters.Select(f => (f.Column, Values: string.Join(',', f.Values), f.Negated)).ShouldBe([("Number", "P", false), ("VerbForm", "Fin", true)]);
    }

    [Fact]
    public async Task MixedSlot_KeepsBothSides()
    {
        IReadOnlyList<GrammarSlotQuery>? seen = null;
        _repository.FindVersesAsync(Arg.Do<IReadOnlyList<GrammarSlotQuery>>(q => seen = q), Arg.Any<CancellationToken>()).Returns([]);

        await _service.SearchAsync("Tempus:aor Modus:partizip dt.Verbform:finit", 1, null, TestContext.Current.CancellationToken);

        var query = seen.ShouldNotBeNull().ShouldHaveSingleItem();
        query.IsGermanOnly.ShouldBeFalse();
        query.Greek.ShouldNotBeNull().Codes.ShouldBe(["V-AAP-NSM"]);
        query.Hebrew.ShouldBeNull();
        query.German!.Filters.Single().Column.ShouldBe("VerbForm");
    }

    [Fact]
    public async Task StsTagFilter_BecomesAEscapedLikePattern()
    {
        IReadOnlyList<GrammarSlotQuery>? seen = null;
        _repository.FindVersesAsync(Arg.Do<IReadOnlyList<GrammarSlotQuery>>(q => seen = q), Arg.Any<CancellationToken>()).Returns([]);

        await _service.SearchAsync("dt.STTS:VV*,N_", 1, null, TestContext.Current.CancellationToken);

        var filter = seen.ShouldNotBeNull().Single().German!.Filters.Single();
        filter.Pattern.ShouldBeTrue();
        filter.Values.ShouldBe(["VV%", "N\\_"]);
    }

    [Fact]
    public async Task MixedSlotWithoutMatchingCode_HasNoHitAndNoQuery()
    {
        var result = await _service.SearchAsync("Tempus:futur dt.Verbform:finit", 1, null, TestContext.Current.CancellationToken);

        result.Response!.TotalVerses.ShouldBe(0);
        await _repository.DidNotReceiveWithAnyArgs().FindVersesAsync(default!, default);
    }

    [Fact]
    public async Task Chapter_GermanOnlySlot_MarksTheGermanWordsOnly()
    {
        _repository.ReadMatchesInRangeAsync(Arg.Any<IReadOnlyList<GrammarSlotQuery>>(), Matt1Start, Matt1Start + 999, Arg.Any<CancellationToken>())
            .Returns([GermanRow(0, 20, 7), GermanRow(0, 24, 5)]);

        var result = await _service.ChapterAsync("dt.Verbform:finit", 40, 1, TestContext.Current.CancellationToken);

        var response = result.Response.ShouldNotBeNull();
        response.Total.ShouldBe(2);
        response.Verses.Select(v => (v.Verse, v.GermanPositions.Single())).ShouldBe([(20, 7), (24, 5)]);
        response.Verses.ShouldAllBe(v => v.OriginalWordIds.Count == 0);
        await _repository.DidNotReceiveWithAnyArgs().ReadMappingsAsync(default!, default);
    }

    [Fact]
    public async Task Chapter_MixedSlot_UsesTheConstrainedGermanWord_NotAllMappedWords()
    {
        _repository.ReadMatchesInRangeAsync(Arg.Any<IReadOnlyList<GrammarSlotQuery>>(), Matt1Start, Matt1Start + 999, Arg.Any<CancellationToken>())
            .Returns([
                new GrammarMatchRow { Slot = 0, HebRefId = Matt1Start + 20, WordId = 324, WordKey = 972, GermanPosition = 7 },
                new GrammarMatchRow { Slot = 0, HebRefId = Matt1Start + 20, WordId = 324, WordKey = 972, GermanPosition = 8 },
            ]);

        var result = await _service.ChapterAsync("Tempus:aor Modus:partizip dt.Verbform:finit", 40, 1, TestContext.Current.CancellationToken);

        var verse = result.Response!.Verses.ShouldHaveSingleItem();
        verse.OriginalWordIds.ShouldBe([324]);
        verse.GermanPositions.ShouldBe([7, 8]);
        result.Response.Total.ShouldBe(1);
    }

    [Fact]
    public async Task Chapter_GermanSlotAndOriginalSlotMayShareAPosition_ButNotAWord()
    {
        _repository.ReadMatchesInRangeAsync(Arg.Any<IReadOnlyList<GrammarSlotQuery>>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([
                GermanRow(0, 12, 9),
                new GrammarMatchRow { Slot = 1, HebRefId = Matt1Start + 12, WordId = 1209, WordKey = 3627 },
                GermanRow(0, 13, 9),
                GermanRow(1, 13, 9),
            ]);

        var result = await _service.ChapterAsync("Liebe agape", 40, 1, TestContext.Current.CancellationToken);

        // Verse 12: a German word and an original word are two words. Verse 13: both slots can only use the same German word.
        result.Response!.Verses.Select(v => v.Verse).ShouldBe([12]);
    }

    [Fact]
    public async Task Facets_GermanKey_CountsGermanWordsByStoredValue()
    {
        _repository.CountGermanAsync("Number", Arg.Any<GrammarGermanSide?>(), Arg.Any<CancellationToken>())
            .Returns([new GrammarCodeCountRow { Code = "S", Count = 150 }, new GrammarCodeCountRow { Code = "P", Count = 7 }]);

        var result = await _service.FacetsAsync("Liebe dt.Numerus:", 0, "dt.Numerus", TestContext.Current.CancellationToken);

        var facet = result.Response!.Facets.ShouldHaveSingleItem();
        facet.Key.ShouldBe("dt.Numerus");
        facet.Values.Select(v => (v.LabelDe, v.Count)).ShouldBe([("Singular", 150), ("Plural", 7)]);
        await _repository.Received(1).CountGermanAsync("Number", Arg.Is<GrammarGermanSide?>(s => s!.Lemma == "liebe" && s.Filters.Count == 0), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Facets_ConjunctionValueSumsBothStoredTags()
    {
        _repository.CountGermanAsync("PartOfSpeech", Arg.Any<GrammarGermanSide?>(), Arg.Any<CancellationToken>())
            .Returns([new GrammarCodeCountRow { Code = "CCONJ", Count = 100 }, new GrammarCodeCountRow { Code = "SCONJ", Count = 20 }]);

        var result = await _service.FacetsAsync("dt.Wortart:", 0, "dt.Wortart", TestContext.Current.CancellationToken);

        result.Response!.Facets.Single().Values.Single().Count.ShouldBe(120);
    }

    [Fact]
    public async Task Facets_UnknownGermanKey_IsUnknownKey()
    {
        var result = await _service.FacetsAsync("Liebe", 0, "dt.Kasus", TestContext.Current.CancellationToken);

        result.Error.ShouldNotBeNull().Code.ShouldBe(GrammarErrorCodes.UnknownKey);
    }

    private static GrammarMatchRow GermanRow(int slot, int verse, int position) => new()
    {
        Slot = slot,
        HebRefId = Matt1Start + verse,
        WordId = verse * 100 + position,
        WordKey = (verse * 100 + position) * 3 + 2,
        Position = position,
        IsGerman = true,
        GermanPosition = position,
    };
}
