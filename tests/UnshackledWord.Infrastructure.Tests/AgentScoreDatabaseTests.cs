using System.Data;
using Dapper;
using Npgsql;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Features.AgentMapping.Scoring;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Infrastructure.Repositories;

namespace UnshackledWord.Infrastructure.Tests;

/// <summary>
/// Roadmap 0018.10 against a real Postgres (migrations up to 0034): the freeze reads a real verse, and the score is stored on a run. Set
/// <c>UNSHACKLEDWORD_TEST_POSTGRES</c> to a COPY of the dev database (e.g. uw_0018_test), otherwise the tests are skipped. With
/// <c>UNSHACKLEDWORD_TEST_REFERENCE_OUT</c> set to a file path the freeze test also writes its result there (scratch use, never into assets).
/// </summary>
public class AgentScoreDatabaseTests
{
    private const string EnvVar = "UNSHACKLEDWORD_TEST_POSTGRES";
    private const string OutVar = "UNSHACKLEDWORD_TEST_REFERENCE_OUT";

    private static string? ConnectionString => Environment.GetEnvironmentVariable(EnvVar);

    private sealed class TestFactory(string connectionString) : IDbConnectionFactory
    {
        public IDbConnection DbConnection { get; set; } = new NpgsqlConnection(connectionString);

        public Task BulkInsertAsync<T>(string tableName, string[] columns, ICollection<T> dataList, Action<IBinaryImporter, T> mapping, CancellationToken token = default) =>
            throw new NotSupportedException();

        public IDbConnection CreateDbConnection() => new NpgsqlConnection(connectionString);

        public async Task<IDbConnection> CreateDbConnectionAsync(CancellationToken token = default)
        {
            var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(token);
            return connection;
        }

        public void Dispose() => DbConnection.Dispose();

        public ValueTask DisposeAsync()
        {
            DbConnection.Dispose();
            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    public async Task Freeze_ExportsMarkOneSeventeenToNineteen_FromTheDatabase()
    {
        Assert.SkipWhen(string.IsNullOrWhiteSpace(ConnectionString), $"{EnvVar} is not set.");
        var freezer = new AgentReferenceFreezer(new AgentDataRepository(new TestFactory(ConnectionString!)), TimeProvider.System);

        var result = await freezer.FreezeAsync(new FreezeAgentReferenceRequest
        {
            Version = "scratch",
            Verses = new[] { 17, 18, 19 }.Select(v => new AgentReferenceVerseKey { BibleBookId = 41, Chapter = 1, Verse = v }).ToList(),
        });

        result.Ok.ShouldBeTrue(string.Join("; ", result.Errors));
        var set = result.Reference!;
        set.Verses.Count.ShouldBe(3);
        set.Verses.ShouldAllBe(v => v.Language == "greek" && v.Words.Count > 3 && v.Links.Count > 3);
        set.Verses.SelectMany(v => v.Links).ShouldAllBe(l => l.Role == "head" || l.Role == "unit");
        ReferenceSetFile.Validate(ReferenceSetFile.Parse(ReferenceSetFile.Serialize(set))).ShouldBeEmpty();

        if (Environment.GetEnvironmentVariable(OutVar) is { Length: > 0 } path)
        {
            File.WriteAllText(path, ReferenceSetFile.Serialize(set));
        }
    }

    [Fact]
    public async Task SaveScore_StoresTheJsonWithTheRun_AndProposalReadsWork()
    {
        Assert.SkipWhen(string.IsNullOrWhiteSpace(ConnectionString), $"{EnvVar} is not set.");
        var factory = new TestFactory(ConnectionString!);
        var repository = new AgentScoreRepository(factory);
        using var connection = await factory.CreateDbConnectionAsync();
        var runId = await connection.ExecuteScalarAsync<int?>("SELECT min(\"Id\") FROM \"unshackled-word\".\"AgentRuns\"");
        Assert.SkipWhen(runId is null, "No agent run in the test database.");

        (await repository.GetProposalsAsync(runId!.Value)).ShouldNotBeNull();
        (await repository.GetPackagesAsync(runId.Value)).ShouldNotBeNull();

        var before = await connection.QuerySingleAsync<(string? Score, DateTime? ScoredAt)>("SELECT \"Score\"::text, \"ScoredAt\" FROM \"unshackled-word\".\"AgentRuns\" WHERE \"Id\" = @Id", new { Id = runId });
        try
        {
            await repository.SaveScoreAsync(runId.Value, "{\"runId\":1}");
            var stored = await connection.ExecuteScalarAsync<string>("SELECT \"Score\"->>'runId' FROM \"unshackled-word\".\"AgentRuns\" WHERE \"Id\" = @Id", new { Id = runId });
            stored.ShouldBe("1");
            (await repository.GetStoredScoreAsync(runId.Value))!.ShouldContain("\"runId\"");
        }
        finally
        {
            await connection.ExecuteAsync("UPDATE \"unshackled-word\".\"AgentRuns\" SET \"Score\" = @Score::jsonb, \"ScoredAt\" = @ScoredAt WHERE \"Id\" = @Id", new { before.Score, before.ScoredAt, Id = runId });
        }
    }
}
