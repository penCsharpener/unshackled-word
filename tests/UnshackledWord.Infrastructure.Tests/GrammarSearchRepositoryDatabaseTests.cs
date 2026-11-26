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
        return new GrammarSlotQuery(codes, lemmaKey is null ? null : [.. GrammarSearchService.StrongsKeys(lemmaKey)]);
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

    private sealed class ConnectionDbReader(IDbConnection connection) : IDbReader
    {
        public Task<T?> ReadFirstOrDefaultAsync<T>(string sql, object? param = null) => connection.QueryFirstOrDefaultAsync<T>(sql, param);

        public async Task<IEnumerable<T>> ReadAsListAsync<T>(string sql, object? param = null) => await connection.QueryAsync<T>(sql, param);

        public Task<T?> ExecuteScalarAsync<T>(string sql, object? param = null) => connection.ExecuteScalarAsync<T>(sql, param);

        public Task<List<T>> ReadMultipleAsListAsync<T>(string sql, object? param, Func<IMultiDbReader, Task<List<T>>> mappingFunc, CancellationToken token = default) =>
            throw new NotSupportedException();
    }
}
