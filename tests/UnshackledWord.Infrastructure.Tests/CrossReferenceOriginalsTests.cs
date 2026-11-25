using System.Data;
using Dapper;
using Npgsql;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Features.CrossReferences;
using UnshackledWord.Domain.Models.Study;
using UnshackledWord.Infrastructure.Repositories;

namespace UnshackledWord.Infrastructure.Tests;

/// <summary>
/// Roadmap 0024.1: the original text of cross reference verses. The pure grouping runs always; the query against the STEP tables needs
/// <c>UNSHACKLEDWORD_TEST_POSTGRES</c> (a copy of the dev database with the STEP import, read only) and is skipped otherwise.
/// </summary>
public class CrossReferenceOriginalsTests
{
    private const string EnvVar = "UNSHACKLEDWORD_TEST_POSTGRES";
    private const int Gen11 = 1001001;
    private const int Joh11 = 43001001;
    private const int Spr822 = 20008022;
    private const int Dan24 = 27002004;

    private static ChapterOriginalWordRow Word(int id, int refId, int position, string text, string? code) =>
        new() { Id = id, HebRefId = refId, Position = position, Original = text, MorphologyCode = code };

    [Fact]
    public void Build_JoinsWordsInReadingOrder_AndMarksGreek()
    {
        var result = CrossReferenceOriginals.Build(
            [Word(2, Joh11, 2, "ἦν", "V-IAI-3S"), Word(1, Joh11, 1, "Ἐν", "PREP"), Word(3, Joh11, 3, "ὁ", "T-NSM")], hebrew: false);

        result[Joh11].Language.ShouldBe("greek");
        result[Joh11].Text.ShouldBe("Ἐν ἦν ὁ");
    }

    private static ChapterOriginalWordRow Keyed(int id, int refId, int position, string? strongs) =>
        new() { Id = id, HebRefId = refId, Position = position, Original = "x", Strongs = strongs };

    [Theory]
    [InlineData("G3004G", "G3004")]
    [InlineData("H1254A", "H1254")]
    [InlineData("H1254", "H1254")]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void BaseNumber_StripsVariantLetters(string? strongs, string? expected)
    {
        CrossReferenceOriginals.BaseNumber(strongs).ShouldBe(expected);
    }

    [Fact]
    public void BuildStrongs_ReturnsDistinctKeysInReadingOrder_AndIgnoresWordsWithoutStrongs()
    {
        var result = CrossReferenceOriginals.BuildStrongs(
            [Keyed(3, Joh11, 3, "G2316"), Keyed(1, Joh11, 1, "G0746"), Keyed(2, Joh11, 2, null), Keyed(4, Joh11, 4, "G0746"), Keyed(5, Spr822, 1, null)],
            new HashSet<string>());

        result[Joh11].ShouldBe(["G0746", "G2316"]);
        result.ContainsKey(Spr822).ShouldBeFalse();
    }

    [Fact]
    public void BuildStrongs_DropsKeysWhoseBaseNumberIsFrequent()
    {
        var result = CrossReferenceOriginals.BuildStrongs(
            [Keyed(1, Joh11, 1, "G2532"), Keyed(2, Joh11, 2, "G3056G"), Keyed(3, Gen11, 1, "H0430A")],
            new HashSet<string> { "G2532", "H0430" });

        result[Joh11].ShouldBe(["G3056G"]);
        result.ContainsKey(Gen11).ShouldBeFalse();
    }

    [Fact]
    public void Build_PicksAramaicByMajorityOfGrammarCodes_AndReturnsOneEntryPerVerse()
    {
        var result = CrossReferenceOriginals.Build(
        [
            Word(1, Dan24, 1, "וַיְדַבְּרוּ", "HVqw3mp"), Word(2, Dan24, 2, "מַלְכָּא", "ANcmsd"), Word(3, Dan24, 3, "חֲלְמָא", "ANcmsd"),
            Word(4, Gen11, 1, "בְּרֵאשִׁ֖ית", "HR/Ncfsa"),
        ], hebrew: true);

        result.Count.ShouldBe(2);
        result[Dan24].Language.ShouldBe("aramaic");
        result[Gen11].Language.ShouldBe("hebrew");
    }

    [Fact]
    public void Build_ReturnsNothingForVersesWithoutWords()
    {
        CrossReferenceOriginals.Build([], hebrew: true).ShouldBeEmpty();
    }

    [Fact]
    public async Task GetOriginalWordsOfVersesAsync_ReturnsHebrewForOldAndGreekForNewTestament_InOneQueryPerTestament() => await WithReaderAsync(async repository =>
    {
        var hebrew = CrossReferenceOriginals.Build(await repository.GetOriginalWordsOfVersesAsync(true, [Gen11, Spr822, Dan24, 1001999]), hebrew: true);
        var greek = CrossReferenceOriginals.Build(await repository.GetOriginalWordsOfVersesAsync(false, [Joh11]), hebrew: false);

        hebrew.Keys.ShouldBe([Gen11, Spr822, Dan24], ignoreOrder: true);
        hebrew[Gen11].Language.ShouldBe("hebrew");
        hebrew[Gen11].Text.ShouldContain("אֱלֹהִ");
        hebrew[Dan24].Language.ShouldBe("aramaic");
        greek.Keys.ShouldBe([Joh11]);
        greek[Joh11].Language.ShouldBe("greek");
        greek[Joh11].Text.ShouldStartWith("Ἐν");
    });

    [Fact]
    public async Task GetOriginalWordsOfVersesAsync_ReturnsOneOriginalPerVerseOfARange() => await WithReaderAsync(async repository =>
    {
        var ids = new[] { 20008022, 20008023, 20008024 };

        var result = CrossReferenceOriginals.Build(await repository.GetOriginalWordsOfVersesAsync(true, ids), hebrew: true);

        result.Keys.OrderBy(k => k).ShouldBe(ids);
        result.Values.ShouldAllBe(o => o.Language == "hebrew" && o.Text.Length > 0);
    });

    [Fact]
    public async Task GetOriginalWordsOfVersesAsync_ReturnsNoWordsForAVerseWithoutOriginals_AndSkipsTheQueryForNoIds() => await WithReaderAsync(async repository =>
    {
        var result = CrossReferenceOriginals.Build(await repository.GetOriginalWordsOfVersesAsync(true, [1001999]), hebrew: true);

        result.ShouldBeEmpty();
        (await repository.GetOriginalWordsOfVersesAsync(false, [])).ShouldBeEmpty();
    });

    private static async Task WithReaderAsync(Func<VerseMappingRepository, Task> test)
    {
        var connectionString = Environment.GetEnvironmentVariable(EnvVar);
        Assert.SkipWhen(string.IsNullOrWhiteSpace(connectionString), $"{EnvVar} is not set.");

        using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        await test(new VerseMappingRepository(new ConnectionDbReader(connection), null!));
    }

    private sealed class ConnectionDbReader(IDbConnection connection) : IDbReader
    {
        public Task<T?> ReadFirstOrDefaultAsync<T>(string sql, object? param = null) => connection.QueryFirstOrDefaultAsync<T>(sql, param);

        public async Task<IEnumerable<T>> ReadAsListAsync<T>(string sql, object? param = null) => await connection.QueryAsync<T>(sql, param);

        public Task<T?> ExecuteScalarAsync<T>(string sql, object? param = null) => connection.ExecuteScalarAsync<T>(sql, param);

        public Task<List<T>> ReadMultipleAsListAsync<T>(string sql, object? param, Func<IMultiDbReader, Task<List<T>>> mappingFunc, CancellationToken token = default) =>
            throw new NotSupportedException();
    }
}
