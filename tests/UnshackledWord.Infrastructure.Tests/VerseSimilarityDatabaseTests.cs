using Dapper;
using Npgsql;
using UnshackledWord.Application.Features.VerseSimilarity;
using UnshackledWord.Infrastructure.VerseSimilarity;

namespace UnshackledWord.Infrastructure.Tests;

/// <summary>
/// Roadmap 0027.2 against a real Postgres with migration 0049: the script runs twice without error, and the pair replace deletes the old rows and
/// writes the new ones across insert batches (types and rounding of the unnest arrays). One rolled-back transaction per test. Skipped without
/// <c>UNSHACKLEDWORD_TEST_POSTGRES</c>.
/// </summary>
public class VerseSimilarityDatabaseTests
{
    private const string EnvVar = "UNSHACKLEDWORD_TEST_POSTGRES";

    private static async Task WithTransactionAsync(Func<NpgsqlConnection, NpgsqlTransaction, Task> test)
    {
        var connectionString = Environment.GetEnvironmentVariable(EnvVar);
        Assert.SkipWhen(string.IsNullOrWhiteSpace(connectionString), $"{EnvVar} is not set.");

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken);

        await test(connection, transaction);

        await transaction.RollbackAsync(TestContext.Current.CancellationToken);
    }

    private static Task<int> NewRunAsync(NpgsqlConnection c, NpgsqlTransaction t) =>
        c.QuerySingleAsync<int>("""
            INSERT INTO "unshackled-word"."VerseSimilarityRuns" ("StartedAt", "Status", "StopDfShare", "TopK", "MinScore", "PhraseWeight")
            VALUES (now(), 'running', 0.02, 30, 0.1, 0.5)
            RETURNING "Id"
            """, null, t);

    [Fact]
    public async Task Migration0049_RunsTwice() => await WithTransactionAsync(async (connection, transaction) =>
    {
        var script = await File.ReadAllTextAsync(Path.Combine(SolutionRoot(), "src", "UnshackledWord.Tooling.Postgres.Migration", "scripts", "0049_VerseSimilarity.sql"),
            TestContext.Current.CancellationToken);

        await connection.ExecuteAsync(script, null, transaction);
        await connection.ExecuteAsync(script, null, transaction);

        (await connection.ExecuteScalarAsync<int>("""
            SELECT count(*)
            FROM information_schema.tables
            WHERE table_schema = 'unshackled-word'
                AND table_name IN ('VerseSimilarityRuns', 'VerseSimilarities')
            """, null, transaction)).ShouldBe(2);
    });

    [Fact]
    public async Task ReplacePairs_DeletesOldRows_AndWritesAllBatches() => await WithTransactionAsync(async (connection, transaction) =>
    {
        var token = TestContext.Current.CancellationToken;
        var first = await NewRunAsync(connection, transaction);
        await VerseSimilarityRepository.ReplacePairsAsync(connection, transaction, first,
            [new VerseSimilarityHit(1_001_001, 1_001_002, 1, 0.5, 2, 1)], token);

        // More rows than one batch, so the loop is exercised; ids are synthetic (no foreign key on the verse ids).
        var second = await NewRunAsync(connection, transaction);
        var hits = Enumerable.Range(0, VerseSimilarityRepository.BatchSize + 5)
            .Select(i => new VerseSimilarityHit(90_000_000 + i / 2, 1_001_001, i % 2 + 1, 0.123456, 3, 0))
            .ToList();
        await VerseSimilarityRepository.ReplacePairsAsync(connection, transaction, second, hits, token);

        var rows = (await connection.QueryAsync<(int RunId, long Count, decimal MaxScore)>("""
            SELECT vs."RunId", count(*), max(vs."Score")
            FROM "unshackled-word"."VerseSimilarities" vs
            GROUP BY vs."RunId"
            """, null, transaction)).ToList();

        rows.ShouldHaveSingleItem().RunId.ShouldBe(second);
        rows[0].Count.ShouldBe(hits.Count);
        rows[0].MaxScore.ShouldBe(0.1235m);
    });

    private static string SolutionRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "UnshackledWord.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Solution root not found.");
    }
}
