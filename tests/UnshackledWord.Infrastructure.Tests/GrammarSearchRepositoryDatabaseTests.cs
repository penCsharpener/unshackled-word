using System.Data;
using Dapper;
using Npgsql;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Features.GrammarSearch;
using UnshackledWord.Domain.Models.Grammar;
using UnshackledWord.Infrastructure.GrammarSearch;

namespace UnshackledWord.Infrastructure.Tests;

/// <summary>
/// Roadmap 0029.1: the slot CTEs of the grammar search against a copy of the dev database with the STEP import and Elb1871 (read only). Skipped without
/// <c>UNSHACKLEDWORD_TEST_POSTGRES</c>. The counts are the ones of the roadmap's example queries.
/// </summary>
public class GrammarSearchRepositoryDatabaseTests
{
    private const string EnvVar = "UNSHACKLEDWORD_TEST_POSTGRES";
    private const int Matt2412 = 40_024_012;

    private static async Task WithDatabaseAsync(Func<GrammarSearchRepository, IReadOnlyList<MorphologyCodeEntry>, Task> test)
    {
        var connectionString = Environment.GetEnvironmentVariable(EnvVar);
        Assert.SkipWhen(string.IsNullOrWhiteSpace(connectionString), $"{EnvVar} is not set.");

        using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        var codes = (await connection.QueryAsync<string>("""
            SELECT DISTINCT w."Morphology"
            FROM "unshackled-word"."StepGreekWords" w
            WHERE w."Morphology" <> ''
            """)).Select(c => new MorphologyCodeEntry(c, GreekMorphologyDecoder.Decode(c))).ToList();

        await test(new GrammarSearchRepository(new ConnectionDbReader(connection)), codes);
    }

    private static GrammarSlotQuery Slot(string query, IReadOnlyList<MorphologyCodeEntry> index, string? lemmaKey)
    {
        var filters = GrammarQueryParser.Parse(query).Slots.Single().Filters;
        var codes = filters.Count == 0 ? null : GrammarFilterResolver.Resolve(filters, index, GrammarVocabulary.LanguageGreek);
        return new GrammarSlotQuery(new GrammarSide(codes, lemmaKey is null ? null : [.. GrammarSearchService.StrongsKeys(lemmaKey)]), null);
    }

    [Fact]
    public async Task LemmaWithCase_FindsTheNominativesOfAgape() => await WithDatabaseAsync(async (repository, index) =>
    {
        var verses = await repository.FindVersesAsync([Slot("Kasus:nom", index, "G0026")], TestContext.Current.CancellationToken);

        verses.Count.ShouldBe(32);
        verses.Sum(v => v.Words).ShouldBe(36);
        verses.ShouldContain(v => v.HebRefId == Matt2412);
        verses.ShouldAllBe(v => !v.Shared);
    });

    [Fact]
    public async Task CountCodes_FoldedByCase_GivesTheFacetOfAgapeWithoutItsOwnFilter() => await WithDatabaseAsync(async (repository, index) =>
    {
        var rows = await repository.CountCodesAsync([.. GrammarSearchService.StrongsKeys("G0026")], TestContext.Current.CancellationToken);
        var facet = GrammarFacetFolder.Fold(GrammarVocabulary.FindKey("Kasus", false)!, rows.ToDictionary(r => r.Code, r => r.Count), index);

        facet.Values.Select(v => (v.LabelDe, v.Count)).ShouldBe([("Nominativ", 36), ("Genitiv", 18), ("Dativ", 29), ("Akkusativ", 33)]);

        var all = await repository.CountCodesAsync(null, TestContext.Current.CancellationToken);
        all.Sum(r => r.Count).ShouldBeGreaterThan(rows.Sum(r => r.Count));
    });

    [Fact]
    public async Task GrammarWithoutLemma_CountsTheAoristPassives() => await WithDatabaseAsync(async (repository, index) =>
    {
        var verses = await repository.FindVersesAsync([Slot("Tempus:aor Diathese:pass", index, null)], TestContext.Current.CancellationToken);

        verses.Count.ShouldBe(1451);
        verses.Sum(v => v.Words).ShouldBe(1706);
    });

    [Fact]
    public async Task TwoSlotsOfTheSameLemma_AreFlaggedShared() => await WithDatabaseAsync(async (repository, index) =>
    {
        var slots = new[] { Slot("Kasus:nom", index, "G0026"), Slot("Kasus:nom", index, "G0026") };

        var verses = await repository.FindVersesAsync(slots, TestContext.Current.CancellationToken);

        verses.ShouldNotBeEmpty();
        verses.ShouldAllBe(v => v.Shared);
    });

    [Fact]
    public async Task MatchesAndMappings_NameTheGermanWordOfMatt2412() => await WithDatabaseAsync(async (repository, index) =>
    {
        var token = TestContext.Current.CancellationToken;
        var matches = await repository.ReadMatchesAsync([Slot("Kasus:nom", index, "G0026")], [Matt2412], token);

        var match = matches.ShouldHaveSingleItem();
        match.Lemma.Normalize(System.Text.NormalizationForm.FormC).ShouldBe("ἀγάπη".Normalize(System.Text.NormalizationForm.FormC));
        match.Morphology.ShouldBe("N-NSF");

        var mapped = await repository.ReadMappingsAsync([match.WordId], token);
        var german = await repository.ReadGermanWordsAsync([Matt2412], token);
        german.Single(g => g.PositionInVerse == mapped.Single().PositionInVerse).WordInContext.ShouldBe("Liebe");
    });

    [Fact]
    public async Task ChapterRange_ReturnsOnlyTheWordsOfThatChapter() => await WithDatabaseAsync(async (repository, index) =>
    {
        var token = TestContext.Current.CancellationToken;

        var matches = await repository.ReadMatchesInRangeAsync([Slot("G26", index, "G0026")], 40_024_000, 40_024_999, token);

        matches.ShouldNotBeEmpty();
        matches.ShouldAllBe(m => m.HebRefId / 1000 == 40_024);
        matches.ShouldContain(m => m.HebRefId == Matt2412);

        var nominatives = await repository.ReadMatchesInRangeAsync([Slot("Kasus:nom", index, "G0026")], 40_024_000, 40_024_999, token);
        nominatives.ShouldHaveSingleItem().HebRefId.ShouldBe(Matt2412);
    });

    [Fact]
    public async Task NearVerses_AreOrderedAwayFromTheBoundary() => await WithDatabaseAsync(async (repository, index) =>
    {
        var token = TestContext.Current.CancellationToken;
        var slots = new[] { Slot("Kasus:nom", index, "G0026") };

        var before = await repository.FindVersesNearAsync(slots, 40_024_000, true, 3, token);
        var after = await repository.FindVersesNearAsync(slots, 40_024_999, false, 3, token);

        before.ShouldBeEmpty();
        after.Count.ShouldBe(3);
        after.Select(v => v.HebRefId).ShouldBe([.. after.Select(v => v.HebRefId).Order()]);
        after.ShouldAllBe(v => v.HebRefId > 40_024_999);
    });

    private static async Task WithHebrewDatabaseAsync(Func<GrammarSearchRepository, IReadOnlyList<MorphologyCodeEntry>, Task> test)
    {
        var connectionString = Environment.GetEnvironmentVariable(EnvVar);
        Assert.SkipWhen(string.IsNullOrWhiteSpace(connectionString), $"{EnvVar} is not set.");

        using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        var codes = (await connection.QueryAsync<string>("""
            SELECT DISTINCT w."Grammar"
            FROM "unshackled-word"."StepHebrewWords" w
            WHERE w."Grammar" <> ''
            """)).Select(c =>
        {
            var decoded = HebrewMorphologyDecoder.Decode(c);
            return new MorphologyCodeEntry(c, decoded.Root, decoded.IsAramaic);
        }).ToList();

        await test(new GrammarSearchRepository(new ConnectionDbReader(connection)), codes);
    }

    private static GrammarSlotQuery HebrewSlot(string query, IReadOnlyList<MorphologyCodeEntry> index, string? lemmaKey)
    {
        var filters = GrammarQueryParser.Parse(query).Slots.Single().Filters;
        var codes = filters.Count == 0 ? null : GrammarFilterResolver.Resolve(filters, index, GrammarVocabulary.LanguageHebrew);
        return new GrammarSlotQuery(null, new GrammarSide(codes, lemmaKey is null ? null : [.. GrammarSearchService.StrongsKeys(lemmaKey)]));
    }

    [Fact]
    public async Task Hebrew_QalOfBara_IsFoundInThirtyThreeVersesStartingWithGenesis1() => await WithHebrewDatabaseAsync(async (repository, index) =>
    {
        var verses = await repository.FindVersesAsync([HebrewSlot("Stamm:qal", index, "H1254")], TestContext.Current.CancellationToken);

        verses.Count.ShouldBe(33);
        verses.Sum(v => v.Words).ShouldBe(39);
        verses.Min(v => v.HebRefId).ShouldBe(1_001_001);
    });

    [Fact]
    public async Task Hebrew_Aramaic_IsFoundInEzraDanielAndJeremiah1011() => await WithHebrewDatabaseAsync(async (repository, index) =>
    {
        var verses = await repository.FindVersesAsync([HebrewSlot("Sprache:aramäisch", index, null)], TestContext.Current.CancellationToken);

        verses.Sum(v => v.Words).ShouldBe(4827);
        verses.GroupBy(v => v.HebRefId / 1_000_000).ToDictionary(g => g.Key, g => g.Sum(v => v.Words)).ShouldBe(new Dictionary<int, int> { [15] = 1212, [24] = 15, [27] = 3600 }, ignoreOrder: true);
    });

    [Fact]
    public async Task Hebrew_ChapterRangeMatchesAndMappings_NameTheGermanWordOfGenesis1() => await WithHebrewDatabaseAsync(async (repository, index) =>
    {
        var token = TestContext.Current.CancellationToken;

        var matches = await repository.ReadMatchesInRangeAsync([HebrewSlot("Stamm:qal", index, "H1254")], 1_001_000, 1_001_999, token);

        matches.Select(m => m.HebRefId % 1000).ShouldBe([1, 21, 27, 27, 27]);
        matches.ShouldAllBe(m => m.IsHebrew && m.Strongs == "H1254A" && m.Morphology.StartsWith('H'));
        var mapped = await repository.ReadHebrewMappingsAsync([.. matches.Select(m => m.WordId)], token);
        var german = await repository.ReadGermanWordsAsync([1_001_001], token);
        german.Single(g => g.PositionInVerse == mapped.First().PositionInVerse).WordInContext.ShouldBe("schuf");
    });

    [Fact]
    public async Task Hebrew_CountCodes_FoldedByStemGivesTheFacetOfBara() => await WithHebrewDatabaseAsync(async (repository, index) =>
    {
        var rows = await repository.CountHebrewCodesAsync([.. GrammarSearchService.StrongsKeys("H1254")], TestContext.Current.CancellationToken);
        var facet = GrammarFacetFolder.Fold(GrammarVocabulary.FindKey("Stamm", false)!, rows.ToDictionary(r => r.Code, r => r.Count), index);

        facet.Values.First().LabelDe.ShouldBe("Qal");
        facet.Values.First().Count.ShouldBe(39);
    });

    // Roadmap 0029.7: German-only and mixed slots.

    private static GrammarSlotQuery GermanSlot(string? lemma, params GrammarGermanFilter[] filters) => new(null, null, new GrammarGermanSide(lemma, filters));

    private static GrammarSlotQuery MixedSlot(string query, IReadOnlyList<MorphologyCodeEntry> index, params GrammarGermanFilter[] filters) =>
        Slot(query, index, null) with { German = new GrammarGermanSide(null, filters) };

    [Fact]
    public async Task German_LemmaAlone_CountsTheWordsWithThatLemma() => await WithDatabaseAsync(async (repository, _) =>
    {
        var verses = await repository.FindVersesAsync([GermanSlot("liebe")], TestContext.Current.CancellationToken);

        verses.Sum(v => v.Words).ShouldBe(184);
        verses.Count.ShouldBe(172);
        verses.ShouldAllBe(v => !v.Shared);
    });

    [Fact]
    public async Task German_LemmaWithNumber_OnlyTheMatchingForms() => await WithDatabaseAsync(async (repository, _) =>
    {
        var token = TestContext.Current.CancellationToken;
        var plural = await repository.FindVersesAsync([GermanSlot("sohn", new GrammarGermanFilter("Number", ["P"], false))], token);
        var notPlural = await repository.FindVersesAsync([GermanSlot("sohn", new GrammarGermanFilter("Number", ["P"], true))], token);

        plural.Sum(v => v.Words).ShouldBe(1234);
        notPlural.Sum(v => v.Words).ShouldBeGreaterThan(1000);

        var matches = await repository.ReadMatchesAsync([GermanSlot("sohn", new GrammarGermanFilter("Number", ["P"], false))], [1_005_004], token);
        var row = matches.ShouldHaveSingleItem();
        row.IsGerman.ShouldBeTrue();
        row.GermanPosition.ShouldBe(16);
        row.Position.ShouldBe(16);
        row.Form.ShouldBe("Söhne");
    });

    [Fact]
    public async Task German_PartOfSpeechWithTwoStoredTags_AndSttsPattern() => await WithDatabaseAsync(async (repository, _) =>
    {
        var token = TestContext.Current.CancellationToken;
        var conjunctions = await repository.FindVersesAsync([GermanSlot(null, new GrammarGermanFilter("PartOfSpeech", ["CCONJ", "SCONJ"], false))], token);
        var participles = await repository.FindVersesAsync([GermanSlot(null, new GrammarGermanFilter("Stts", ["VVPP"], false, Pattern: true))], token);
        var verbs = await repository.FindVersesAsync([GermanSlot(null, new GrammarGermanFilter("Stts", ["VV%"], false, Pattern: true), new GrammarGermanFilter("VerbForm", ["Fin"], true))], token);

        conjunctions.Sum(v => v.Words).ShouldBe(78456);
        participles.Sum(v => v.Words).ShouldBe(17265);
        verbs.Sum(v => v.Words).ShouldBeGreaterThan(30000);
    });

    [Fact]
    public async Task Mixed_GreekAoristParticipleRenderedByAFiniteVerb() => await WithDatabaseAsync(async (repository, index) =>
    {
        var token = TestContext.Current.CancellationToken;
        var slot = MixedSlot("Tempus:aor Modus:partizip", index, new GrammarGermanFilter("VerbForm", ["Fin"], false));

        var verses = await repository.FindVersesAsync([slot], token);

        verses.Sum(v => v.Words).ShouldBe(1478);
        verses.Count.ShouldBe(1247);
        verses.ShouldAllBe(v => !v.Shared);

        var matches = await repository.ReadMatchesAsync([slot], [40_001_020], token);
        var row = matches.ShouldHaveSingleItem();
        row.IsGerman.ShouldBeFalse();
        row.Strongs.ShouldBe("G1760");
        row.GermanPosition.ShouldBe(7);
    });

    [Fact]
    public async Task Mixed_GermanLemmaOfAnOriginalWord() => await WithDatabaseAsync(async (repository, index) =>
    {
        var token = TestContext.Current.CancellationToken;
        var slot = Slot("Kasus:nom", index, "G0026") with { German = new GrammarGermanSide("liebe", []) };

        var verses = await repository.FindVersesAsync([slot], token);

        verses.ShouldContain(v => v.HebRefId == Matt2412);
        verses.Sum(v => v.Words).ShouldBeLessThanOrEqualTo(36);
    });

    [Fact]
    public async Task GermanSlotAndOriginalSlot_AreDifferentWordsEvenAtTheSamePosition() => await WithDatabaseAsync(async (repository, index) =>
    {
        var verses = await repository.FindVersesAsync([GermanSlot("liebe"), Slot("Kasus:nom", index, "G0026")], TestContext.Current.CancellationToken);

        var matt = verses.Single(v => v.HebRefId == Matt2412);
        matt.Shared.ShouldBeFalse();
        matt.Words.ShouldBe(2);
    });

    [Fact]
    public async Task Near_AndRange_WorkForGermanSlots() => await WithDatabaseAsync(async (repository, _) =>
    {
        var token = TestContext.Current.CancellationToken;
        var slots = new[] { GermanSlot("liebe") };

        var next = await repository.FindVersesNearAsync(slots, Matt2412, false, 1, token);
        var inRange = await repository.ReadMatchesInRangeAsync(slots, 40_024_000, 40_024_999, token);

        next.ShouldHaveSingleItem().HebRefId.ShouldBeGreaterThan(Matt2412);
        inRange.ShouldAllBe(m => m.IsGerman && m.HebRefId / 1000 == 40_024);
        inRange.ShouldContain(m => m.HebRefId == Matt2412 && m.Position == 9);
    });

    [Fact]
    public async Task CountGerman_FoldsTheStoredValuesOfAColumn() => await WithDatabaseAsync(async (repository, _) =>
    {
        var token = TestContext.Current.CancellationToken;
        var all = await repository.CountGermanAsync("VerbForm", null, token);
        var liebe = await repository.CountGermanAsync("PartOfSpeech", new GrammarGermanSide("liebe", []), token);

        all.ToDictionary(r => r.Code, r => r.Count).ShouldBe(new Dictionary<string, int> { ["Fin"] = 95423, ["Inf"] = 18528, ["Part"] = 17862 });
        liebe.ToDictionary(r => r.Code, r => r.Count).ShouldBe(new Dictionary<string, int> { ["NOUN"] = 157, ["VERB"] = 27 });
        (await repository.GermanLemmaExistsAsync("liebe", token)).ShouldBeTrue();
        (await repository.GermanLemmaExistsAsync("xyzzyq", token)).ShouldBeFalse();
    });

    private sealed class ConnectionDbReader(IDbConnection connection) : IDbReader
    {
        public Task<T?> ReadFirstOrDefaultAsync<T>(string sql, object? param = null) => connection.QueryFirstOrDefaultAsync<T>(sql, param);

        public async Task<IEnumerable<T>> ReadAsListAsync<T>(string sql, object? param = null) => await connection.QueryAsync<T>(sql, param);

        public Task<T?> ExecuteScalarAsync<T>(string sql, object? param = null) => connection.ExecuteScalarAsync<T>(sql, param);

        public Task<List<T>> ReadMultipleAsListAsync<T>(string sql, object? param, Func<IMultiDbReader, Task<List<T>>> mappingFunc, CancellationToken token = default) =>
            throw new NotSupportedException();
    }
}
