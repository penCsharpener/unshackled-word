using System.Data;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dapper;
using Npgsql;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Features.CrossReferences;
using UnshackledWord.Application.Repositories;
using UnshackledWord.Domain.Models.Study;
using UnshackledWord.Infrastructure.Repositories;
using UnshackledWord.TestTooling;

namespace UnshackledWord.Infrastructure.Tests;

/// <summary>
/// Roadmap 0024.2: the stored TSK keys (migration 0044, <see cref="TskKeySql"/>) against the former per-request translation
/// (<see cref="LegacyTskRepository"/>), over every chapter of the Bible. Needs <c>UNSHACKLEDWORD_TEST_POSTGRES</c> pointing at a seeded database
/// with migration 0044 applied (read only); skipped otherwise. The script/C# comparison runs always.
/// </summary>
public partial class TskKeysDatabaseTests
{
    private const string EnvVar = "UNSHACKLEDWORD_TEST_POSTGRES";

    [Theory]
    [InlineData("TskKeySql.CopyMultiMappedSources", TskKeySql.CopyMultiMappedSources)]
    [InlineData("TskKeySql.FillKeys", TskKeySql.FillKeys)]
    public void MigrationScript_HoldsTheSameKeySqlAsTheSeed(string marker, string sql)
    {
        var script = File.ReadAllText(Path.Combine(SolutionRoot(), "src", "UnshackledWord.Tooling.Postgres.Migration", "scripts", "0044_TskKeys.sql"));
        var start = script.IndexOf("-- " + marker + "\n", StringComparison.Ordinal);
        start.ShouldBeGreaterThan(0, $"marker '{marker}' missing in 0044_TskKeys.sql");
        start = script.IndexOf('\n', start) + 1;
        var statement = script[start..(script.IndexOf(';', start) + 1)];

        Normalise(statement).ShouldBe(Normalise(sql));
    }

    [Fact]
    public async Task EveryChapter_CrossReferencesEqualTheFormerReadPath() => await WithReaderAsync(async reader =>
    {
        var chapters = (await reader.ReadAsListAsync<int>("""
            SELECT DISTINCT "HebRefId" / 1000
            FROM "unshackled-word"."BibleVerseCountingMapping"
            ORDER BY 1
            """)).ToList();
        var legacy = new LegacyTskRepository(reader);
        var current = new TskRepository(reader);
        var legacyService = new ChapterCrossReferenceService(legacy);
        var currentService = new ChapterCrossReferenceService(current);
        int rows = 0, references = 0, chaptersWithReferences = 0, maxRows = 0;
        var differences = new List<string>();

        foreach (var bookChapter in chapters)
        {
            var (book, chapter) = (bookChapter / 1000, bookChapter % 1000);
            var from = bookChapter * 1000;
            var to = from + 999;

            // Rows before the chapter's de-duplication: same sources, keywords and normalised refs in the same order.
            var before = (await legacy.GetOutgoingAsync(from, to, ChapterCrossReferenceService.MaxRows)).Select(Describe).ToList();
            var after = (await current.GetOutgoingAsync(from, to, ChapterCrossReferenceService.MaxRows)).Select(Describe).ToList();
            var beforeResponse = JsonSerializer.Serialize(await legacyService.GetAsync(book, chapter));
            var afterResponse = JsonSerializer.Serialize(await currentService.GetAsync(book, chapter));

            if (!before.SequenceEqual(after) || beforeResponse != afterResponse)
            {
                differences.Add($"{book} {chapter}: rows {before.Count} -> {after.Count}");
            }

            rows += before.Count;
            maxRows = Math.Max(maxRows, before.Count);
            chaptersWithReferences += before.Count > 0 ? 1 : 0;
            references += (await currentService.GetAsync(book, chapter)).Verses.Sum(v => v.References.Count);
        }

        TestContext.Current.TestOutputHelper?.WriteLine(
            $"chapters {chapters.Count}, with references {chaptersWithReferences}, rows {rows}, references after de-duplication {references}, busiest chapter {maxRows} rows");
        differences.ShouldBeEmpty();
        maxRows.ShouldBeLessThan(ChapterCrossReferenceService.MaxRows);
        rows.ShouldBeGreaterThan(300_000);
    });

    [Fact]
    public async Task SourceMappedToTwoHebrewVerses_ShowsItsRowsUnderBoth() => await WithReaderAsync(async reader =>
    {
        // KJV 1Sam 20,42 is Hebrew 20,42 + 21,1; before 0044 the INNER JOIN listed its six rows under both verses.
        var repository = new TskRepository(reader);

        var first = (await repository.GetOutgoingAsync(9020042, 9020042, 100)).Select(r => (r.Scope, r.TargetStartHebRefId, r.TargetEndHebRefId)).ToList();
        var second = (await repository.GetOutgoingAsync(9021001, 9021001, 100)).Select(r => (r.Scope, r.TargetStartHebRefId, r.TargetEndHebRefId)).ToList();

        first.Count.ShouldBe(6);
        second.ShouldBe(first);
    });

    [Fact]
    public async Task StoredKeys_AreNormalisedLikeCrossReferenceMapper() => await WithReaderAsync(async reader =>
    {
        // No backward range, no single-verse "range" and no range across chapters is left in the table.
        var invalid = await reader.ExecuteScalarAsync<long>("""
            SELECT COUNT(*)
            FROM "unshackled-word"."Tsk" t
            WHERE 1=1
                AND t."TargetEndHebRefId" IS NOT NULL
                AND (t."TargetEndHebRefId" <= t."TargetStartHebRefId" OR t."TargetEndHebRefId" / 1000 <> t."TargetStartHebRefId" / 1000)
            """);
        var withoutTarget = await reader.ExecuteScalarAsync<long>("""
            SELECT COUNT(*)
            FROM "unshackled-word"."Tsk" t
            WHERE t."TargetStartHebRefId" IS NULL
            """);

        invalid.ShouldBe(0);
        withoutTarget.ShouldBe(0);
    });

    [Fact]
    public async Task Psalm119_ChapterQuery_Timing() => await WithReaderAsync(async reader =>
    {
        // Not an assertion on speed: prints the median of 20 warm runs of the former and the new chapter query for the report.
        var legacy = await MedianAsync(new LegacyTskRepository(reader));
        var current = await MedianAsync(new TskRepository(reader));

        TestContext.Current.TestOutputHelper?.WriteLine($"Ps 119 chapter query, median of 20: former {legacy:F2} ms, stored keys {current:F2} ms");
        current.ShouldBeGreaterThan(0);
    });

    private static async Task<double> MedianAsync(ITskRepository repository)
    {
        var times = new List<double>();
        for (var i = 0; i < 25; i++)
        {
            var watch = Stopwatch.StartNew();
            var rows = await repository.GetOutgoingAsync(19_119_000, 19_119_999, ChapterCrossReferenceService.MaxRows);
            watch.Stop();
            rows.Count.ShouldBeGreaterThan(1000);
            if (i >= 5)
            {
                times.Add(watch.Elapsed.TotalMilliseconds);
            }
        }

        times.Sort();
        return (times[9] + times[10]) / 2;
    }

    private static string Describe(TskCrossReference row)
    {
        var reference = CrossReferenceMapper.Build(row);
        return $"{row.SourceHebRefId}|{row.Scope}|{reference.BibleBookId}.{reference.Chapter}.{reference.Verse}-{reference.EndVerse}";
    }

    private static string Normalise(string sql) => WhitespaceRegex().Replace(sql, " ").Trim();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    private static string SolutionRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "UnshackledWord.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Solution root not found.");
    }

    private static async Task WithReaderAsync(Func<IDbReader, Task> test)
    {
        var connectionString = Environment.GetEnvironmentVariable(EnvVar);
        Assert.SkipWhen(string.IsNullOrWhiteSpace(connectionString), $"{EnvVar} is not set.");

        using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        await test(new ConnectionDbReader(connection));
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
