using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using UnshackledWord.Application.Features.AgentMapping;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Infrastructure.Repositories;
using UnshackledWord.Infrastructure.Vocab;
using UnshackledWord.Persistence.Postgres.Services;

namespace UnshackledWord.Infrastructure.Tests;

/// <summary>
/// The vocab agent runs (form: roadmap 0022.13, root: 0030.4) cover a whole from..to range when the request has no limit (no cut at 200),
/// estimate and start select the same entries, and the maintainer-set batch size drives the estimate and is stored on the run.
/// Set <c>UNSHACKLEDWORD_TEST_POSTGRES</c>, otherwise the tests are skipped.
/// </summary>
[Collection("VocabDatabase")]
public class VocabAgentRunRangeDatabaseTests
{
    private const string EnvVar = "UNSHACKLEDWORD_TEST_POSTGRES";
    private const int ToRank = 400;
    private const int BatchSize = 40;

    private static string? ConnectionString => Environment.GetEnvironmentVariable(EnvVar);

    private static IConfiguration Configuration() => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["ConnectionStrings:PostgresConnection"] = ConnectionString,
        ["AgentMapping:Profiles:stub:Kind"] = "ollama",
        ["AgentMapping:Profiles:stub:BaseUrl"] = "http://localhost:11434",
        ["AgentMapping:Profiles:stub:Model"] = "stub-model",
    }).Build();

    private static async Task<int> UserIdAsync(NpgsqlConnection connection) =>
        await connection.QueryFirstAsync<int>("""SELECT "Id" FROM "unshackled-word"."Users" ORDER BY "Id" LIMIT 1""");

    private static Task DeleteRunAsync(NpgsqlConnection connection, int runId) =>
        connection.ExecuteAsync("""DELETE FROM "unshackled-word"."AgentRuns" WHERE "Id" = @Id""", new { Id = runId });

    [Fact]
    public async Task FormRun_WithoutLimit_CoversTheWholeRangeAndStoresTheBatchSize()
    {
        Assert.SkipWhen(string.IsNullOrWhiteSpace(ConnectionString), $"{EnvVar} is not set.");

        var configuration = Configuration();
        var factory = new PostgresDbConnectionFactory(configuration, NullLogger<PostgresDbConnectionFactory>.Instance);
        var forms = new VocabFormRepository(factory);
        var runs = new AgentRunRepository(factory);
        var service = new VocabFormRunService(forms, runs, new AgentStoreRepository(factory), new AgentPromptProvider(AgentPromptProvider.ResolveDirectory(configuration)), configuration);
        var token = TestContext.Current.CancellationToken;
        await using var connection = new NpgsqlConnection(ConnectionString);

        var course = (await forms.GetCourseAsync("nt-greek", null, token))!;
        var all = await forms.SelectCandidatesAsync(course.Track, 1, ToRank, int.MaxValue, token);
        Assert.SkipWhen(all.Count == 0, "No form candidates in ranks 1..400.");

        var estimate = await service.EstimateAsync(new VocabFormRunRangeRequest { Course = "nt-greek", FromRank = 1, ToRank = ToRank, BatchSize = BatchSize, Profile = "stub" }, null, null, token);
        estimate.Outcome.ShouldBe(AgentRunPrepareOutcome.Ok, estimate.Error);
        estimate.Estimate!.Entries.ShouldBe(all.Count);
        estimate.Estimate.BatchSize.ShouldBe(BatchSize);
        estimate.Estimate.Batches.ShouldBe((all.Count + BatchSize - 1) / BatchSize);
        estimate.Estimate.ToRank.ShouldBe(all[^1].Rank);

        var defaultBatch = await service.EstimateAsync(new VocabFormRunRangeRequest { Course = "nt-greek", FromRank = 1, ToRank = ToRank, Profile = "stub" }, null, null, token);
        defaultBatch.Estimate!.BatchSize.ShouldBe(VocabFormRunExecutor.DefaultBatchSize);
        defaultBatch.Estimate.Batches.ShouldBe((all.Count + 24) / 25);

        var created = await service.CreateRunAsync(new StartVocabFormRunRequest { Course = "nt-greek", FromRank = 1, ToRank = ToRank, BatchSize = BatchSize, Profile = "stub" }, await UserIdAsync(connection), token);
        created.Outcome.ShouldBe(AgentRunPrepareOutcome.Ok, created.Error);

        try
        {
            var run = (await runs.GetRunAsync(created.RunId, token))!;
            run.BatchSize.ShouldBe(BatchSize);
            run.FromRefId.ShouldBe(all[0].Rank);
            run.ToRefId.ShouldBe(all[^1].Rank);
        }
        finally
        {
            await DeleteRunAsync(connection, created.RunId);
        }
    }

    [Fact]
    public async Task RootRun_WithoutLimit_CoversTheWholeRangeAndStoresTheBatchSize()
    {
        Assert.SkipWhen(string.IsNullOrWhiteSpace(ConnectionString), $"{EnvVar} is not set.");

        var configuration = Configuration();
        var factory = new PostgresDbConnectionFactory(configuration, NullLogger<PostgresDbConnectionFactory>.Instance);
        var forms = new VocabFormRepository(factory);
        var roots = new VocabRootRunRepository(factory);
        var runs = new AgentRunRepository(factory);
        var service = new VocabRootRunService(forms, roots, runs, new AgentStoreRepository(factory), new AgentPromptProvider(AgentPromptProvider.ResolveDirectory(configuration)), configuration);
        var token = TestContext.Current.CancellationToken;
        await using var connection = new NpgsqlConnection(ConnectionString);

        var course = (await forms.GetCourseAsync("nt-greek", null, token))!;
        // with "Vorbefüllung prüfen" the prefill rows count too, so the dev DB has candidates even after a root run
        var all = await roots.SelectCandidatesAsync(course.Track, 1, ToRank, int.MaxValue, true, token);
        Assert.SkipWhen(all.Count == 0, "No root candidates in ranks 1..400.");

        var estimate = await service.EstimateAsync(new VocabRootRunRangeRequest { Course = "nt-greek", FromRank = 1, ToRank = ToRank, BatchSize = BatchSize, CheckPrefill = true, Profile = "stub" }, null, null, token);
        estimate.Outcome.ShouldBe(AgentRunPrepareOutcome.Ok, estimate.Error);
        estimate.Estimate!.Entries.ShouldBe(all.Count);
        estimate.Estimate.BatchSize.ShouldBe(BatchSize);
        estimate.Estimate.Batches.ShouldBe((all.Count + BatchSize - 1) / BatchSize);

        var created = await service.CreateRunAsync(new StartVocabRootRunRequest { Course = "nt-greek", FromRank = 1, ToRank = ToRank, BatchSize = BatchSize, CheckPrefill = true, Profile = "stub" }, await UserIdAsync(connection), token);
        created.Outcome.ShouldBe(AgentRunPrepareOutcome.Ok, created.Error);

        try
        {
            var run = (await runs.GetRunAsync(created.RunId, token))!;
            run.BatchSize.ShouldBe(BatchSize);
            run.ToRefId.ShouldBe(all[^1].Rank);
        }
        finally
        {
            await DeleteRunAsync(connection, created.RunId);
        }
    }

    [Fact]
    public async Task Runs_RejectABatchSizeOutside1To200()
    {
        Assert.SkipWhen(string.IsNullOrWhiteSpace(ConnectionString), $"{EnvVar} is not set.");

        var configuration = Configuration();
        var factory = new PostgresDbConnectionFactory(configuration, NullLogger<PostgresDbConnectionFactory>.Instance);
        var prompts = new AgentPromptProvider(AgentPromptProvider.ResolveDirectory(configuration));
        var formService = new VocabFormRunService(new VocabFormRepository(factory), new AgentRunRepository(factory), new AgentStoreRepository(factory), prompts, configuration);
        var rootService = new VocabRootRunService(new VocabFormRepository(factory), new VocabRootRunRepository(factory), new AgentRunRepository(factory), new AgentStoreRepository(factory), prompts, configuration);
        var token = TestContext.Current.CancellationToken;

        (await formService.EstimateAsync(new VocabFormRunRangeRequest { Course = "nt-greek", BatchSize = 201, Profile = "stub" }, null, null, token)).Outcome.ShouldBe(AgentRunPrepareOutcome.BadRequest);
        (await rootService.EstimateAsync(new VocabRootRunRangeRequest { Course = "nt-greek", BatchSize = 0, Profile = "stub" }, null, null, token)).Outcome.ShouldBe(AgentRunPrepareOutcome.BadRequest);
    }
}
