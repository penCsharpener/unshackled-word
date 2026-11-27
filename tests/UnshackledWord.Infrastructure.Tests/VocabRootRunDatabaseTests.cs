using System.Data;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using UnshackledWord.Application.Features.AgentMapping;
using UnshackledWord.Application.Features.Vocab.Roots;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Infrastructure.Repositories;
using UnshackledWord.Infrastructure.Vocab;
using UnshackledWord.Persistence.Postgres.Services;

namespace UnshackledWord.Infrastructure.Tests;

/// <summary>
/// Roadmap 0030.4: the root writes of the agent run (<see cref="VocabRootRunWriter"/>, one rolled-back transaction per test) and the whole
/// <see cref="VocabRootRunExecutor"/> with a stub model client (no provider call, no cost; the run cleans up after itself).
/// Set <c>UNSHACKLEDWORD_TEST_POSTGRES</c>, otherwise the tests are skipped.
/// </summary>
[Collection("VocabDatabase")]
public class VocabRootRunDatabaseTests
{
    private const string EnvVar = "UNSHACKLEDWORD_TEST_POSTGRES";
    private const string Keys = "'G3056', 'G3140', 'G4901', 'G4198', 'G1607', 'H3789', 'H4385'";

    private static string? ConnectionString => Environment.GetEnvironmentVariable(EnvVar);

    private static async Task WithTransactionAsync(Func<IDbConnection, IDbTransaction, Task> test)
    {
        Assert.SkipWhen(string.IsNullOrWhiteSpace(ConnectionString), $"{EnvVar} is not set.");

        using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction();

        await ClearAsync(connection, transaction);
        await test(connection, transaction);

        transaction.Rollback();
    }

    /// <summary>Removes the rows and roots of the test keys (inside the caller's transaction, so a rollback restores them).</summary>
    private static Task<int> ClearAsync(IDbConnection c, IDbTransaction? t) => c.ExecuteAsync($"""
        DELETE FROM "unshackled-word"."VocabStrongsRoots" WHERE "StrongsKey" IN ({Keys})
            OR "VocabRootId" IN (SELECT "Id" FROM "unshackled-word"."VocabRoots" WHERE "StrongsKey" IN ({Keys}));
        UPDATE "unshackled-word"."VocabRoots" SET "ParentRootId" = NULL
        WHERE "ParentRootId" IN (SELECT "Id" FROM "unshackled-word"."VocabRoots" WHERE "StrongsKey" IN ({Keys}));
        DELETE FROM "unshackled-word"."VocabRoots" WHERE "StrongsKey" IN ({Keys});
        """, transaction: t);

    private static VocabRootInput Entry(string lemma, string key, VocabRootExisting? existing = null) => new(1, lemma, key, null, null, null, existing);

    private sealed class WordRoot
    {
        public string Root { get; set; } = string.Empty;
        public string? RootKey { get; set; }
        public string? Prefixes { get; set; }
        public string[]? PrefixKeys { get; set; }
        public string Origin { get; set; } = string.Empty;
        public int? AgentRunId { get; set; }
    }

    private static Task<WordRoot> WordRootAsync(IDbConnection c, IDbTransaction? t, string key) => c.QuerySingleAsync<WordRoot>("""
        SELECT r."Root", r."StrongsKey" AS "RootKey", s."Prefixes", s."PrefixKeys", s."Origin", s."AgentRunId"
        FROM "unshackled-word"."VocabStrongsRoots" s
            JOIN "unshackled-word"."VocabRoots" r ON r."Id" = s."VocabRootId"
        WHERE s."StrongsKey" = @Key
        """, new { Key = key }, t);

    [Fact]
    public Task Writer_Simple_CreatesTheRootOfTheWordItself() => WithTransactionAsync(async (c, t) =>
    {
        var plan = new VocabRootPlan("λόγος", "G3056", [], null, true);

        var result = await VocabRootRunWriter.ApplyAsync(c, t, 'G', Entry("λόγος", "G3056"), plan, 900001, null);

        result.Status.ShouldBe(VocabRootWriteStatus.Written);
        var row = await WordRootAsync(c, t, "G3056");
        row.Root.ShouldBe("λόγος");
        row.RootKey.ShouldBe("G3056");
        row.Prefixes.ShouldBeNull();
        row.Origin.ShouldBe("agent");
        row.AgentRunId.ShouldBe(900001);
    });

    [Fact]
    public Task Writer_Compound_CreatesTheRootOnceAndKeepsThePrefixes() => WithTransactionAsync(async (c, t) =>
    {
        var plan = new VocabRootPlan("μαρτυρέω", "G3140", ["G4862", "G1909"], "σύν + ἐπί", false);

        (await VocabRootRunWriter.ApplyAsync(c, t, 'G', Entry("συνεπιμαρτυρέω", "G4901"), plan, 900001, null)).Status.ShouldBe(VocabRootWriteStatus.Written);
        (await VocabRootRunWriter.ApplyAsync(c, t, 'G', Entry("ἐπιμαρτυρέω", "G4901"), plan, 900001, null)).Status.ShouldBe(VocabRootWriteStatus.Written);

        var row = await WordRootAsync(c, t, "G4901");
        row.RootKey.ShouldBe("G3140");
        row.Prefixes.ShouldBe("σύν + ἐπί");
        row.PrefixKeys.ShouldBe(["G4862", "G1909"]);
        (await c.QuerySingleAsync<int>("""SELECT count(*)::int FROM "unshackled-word"."VocabRoots" WHERE "StrongsKey" = 'G3140'""", transaction: t)).ShouldBe(1);
    });

    [Fact]
    public Task Writer_ManualRow_IsNeverReplaced() => WithTransactionAsync(async (c, t) =>
    {
        var rootId = await VocabRootRepository.UpsertRootAsync(c, t, new VocabRootUpsert { Track = "G", Root = "μαρτυρέω", StrongsKey = "G3140" });
        await VocabRootRepository.UpsertWordRootAsync(c, t, new VocabWordRootUpsert { Lang = "G", StrongsKey = "G4901", VocabRootId = rootId, Origin = "manual" });
        var existing = new VocabRootExisting("manual", null, [], "μαρτυρέω", "G3140");

        var result = await VocabRootRunWriter.ApplyAsync(c, t, 'G', Entry("συνεπιμαρτυρέω", "G4901", existing), new VocabRootPlan("συνεπιμαρτυρέω", "G4901", [], null, true), 900001, null);

        result.Status.ShouldBe(VocabRootWriteStatus.Skipped);
        (await WordRootAsync(c, t, "G4901")).Origin.ShouldBe("manual");
    });

    [Fact]
    public Task Writer_PrefillRow_IsReplacedAndListedOrConfirmed() => WithTransactionAsync(async (c, t) =>
    {
        var rootId = await VocabRootRepository.UpsertRootAsync(c, t, new VocabRootUpsert { Track = "G", Root = "μαρτυρέω", StrongsKey = "G3140" });
        await VocabRootRepository.UpsertWordRootAsync(c, t, new VocabWordRootUpsert { Lang = "G", StrongsKey = "G4901", VocabRootId = rootId, Prefixes = "σύν", PrefixKeys = ["G4862"], Origin = "prefill" });
        var existing = new VocabRootExisting("prefill", "σύν", ["G4862"], "μαρτυρέω", "G3140");
        var entry = Entry("συνεπιμαρτυρέω", "G4901", existing);

        var same = await VocabRootRunWriter.ApplyAsync(c, t, 'G', entry, new VocabRootPlan("μαρτυρέω", "G3140", ["G4862"], "σύν", false), 900001, null);
        same.Status.ShouldBe(VocabRootWriteStatus.Confirmed);
        (await WordRootAsync(c, t, "G4901")).Origin.ShouldBe("agent");

        var changed = await VocabRootRunWriter.ApplyAsync(c, t, 'G', entry, new VocabRootPlan("μαρτυρέω", "G3140", ["G4862", "G1909"], "σύν + ἐπί", false), 900002, null);
        changed.Status.ShouldBe(VocabRootWriteStatus.Replaced);
        changed.Reason.ShouldContain("σύν + ἐπί");
        (await WordRootAsync(c, t, "G4901")).PrefixKeys.ShouldBe(["G4862", "G1909"]);
    });

    [Fact]
    public Task Writer_Hebrew_UsesTheVerbRootOrTheOnlyRootWithTheLetters() => WithTransactionAsync(async (c, t) =>
    {
        var withVerb = new VocabRootPlan("כתב", "H3789", [], null, false);
        (await VocabRootRunWriter.ApplyAsync(c, t, 'H', Entry("מִכְתָּב", "H4385"), withVerb, 900001, null)).Status.ShouldBe(VocabRootWriteStatus.Written);

        var row = await WordRootAsync(c, t, "H4385");
        row.RootKey.ShouldBe("H3789");
        row.Origin.ShouldBe("agent");

        // a second word with the letters only joins the verb's root while exactly one root has them
        var letters = new VocabRootPlan("כתב", null, [], null, false);
        var rootCount = await c.QuerySingleAsync<int>("""SELECT count(*)::int FROM "unshackled-word"."VocabRoots" WHERE "Track" = 'H' AND "RootNormalized" = 'כתב'""", transaction: t);
        if (rootCount == 1)
        {
            (await VocabRootRunWriter.ApplyAsync(c, t, 'H', Entry("כָּתוּב", "H3789"), letters, 900001, null)).Status.ShouldBe(VocabRootWriteStatus.Written);
        }
    });

    /// <summary>Answers like a model: the run's entries are decided from the lemma; the first answer for the compound invents a prefix key, the retry is right.</summary>
    private sealed class StubClient : IAgentModelClient, IAgentModelClientFactory
    {
        public int Calls { get; private set; }

        public IAgentModelClient Create(AgentProfile profile) => this;

        public Task<AgentModelResult> CompleteAsync(AgentModelRequest request, CancellationToken token)
        {
            Calls++;
            using var package = JsonDocument.Parse(request.PackageJson);
            var rows = new List<object>();

            foreach (var entry in package.RootElement.GetProperty("entries").EnumerateArray())
            {
                var id = entry[0].GetString()!;
                var lemma = UnshackledWord.Domain.Models.Vocab.VocabText.Normalize(entry[1].GetString());
                var retry = entry[7].ValueKind == JsonValueKind.String;

                rows.Add(lemma switch
                {
                    "λογοσ" => new { id, simple = true, prefixes = Array.Empty<string>(), confidence = "high", note = string.Empty },
                    "συνεπιμαρτυρεω" when retry => new { id, simple = false, prefixes = new[] { "G4862", "G1909" }, rootKey = "G3140", confidence = "high", note = string.Empty },
                    "συνεπιμαρτυρεω" => new { id, simple = false, prefixes = new[] { "G9999" }, rootKey = "G3140", confidence = "high", note = string.Empty },
                    _ => (object)new { id, simple = false, prefixes = new[] { "G1537" }, rootKey = "G3056", confidence = "high", note = "unsure" },
                });
            }

            return Task.FromResult(new AgentModelResult { Json = JsonSerializer.Serialize(new { roots = rows }), InputTokens = 1000, OutputTokens = 100, Model = request.Model });
        }
    }

    [Fact]
    public async Task Executor_WithAStubModel_WritesAgentRowsRetriesInventedKeysAndReportsReplacements()
    {
        Assert.SkipWhen(string.IsNullOrWhiteSpace(ConnectionString), $"{EnvVar} is not set.");

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:PostgresConnection"] = ConnectionString,
            ["AgentMapping:Profiles:stub:Kind"] = "ollama",
            ["AgentMapping:Profiles:stub:BaseUrl"] = "http://localhost:11434",
            ["AgentMapping:Profiles:stub:Model"] = "stub-model",
        }).Build();
        var factory = new PostgresDbConnectionFactory(configuration, NullLogger<PostgresDbConnectionFactory>.Instance);
        var forms = new VocabFormRepository(factory);
        var roots = new VocabRootRunRepository(factory);
        var runs = new AgentRunRepository(factory);
        var store = new AgentStoreRepository(factory);
        var prompts = new AgentPromptProvider(AgentPromptProvider.ResolveDirectory(configuration));
        var service = new VocabRootRunService(forms, roots, runs, store, prompts, configuration);
        var stub = new StubClient();
        var executor = new VocabRootRunExecutor(forms, roots, factory, runs, stub, prompts, configuration, NullLogger<VocabRootRunExecutor>.Instance);

        using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        var userId = await connection.QueryFirstAsync<int>("""SELECT "Id" FROM "unshackled-word"."Users" ORDER BY "Id" LIMIT 1""");
        var maxRootId = await connection.QuerySingleAsync<int>("""SELECT COALESCE(max("Id"), 0) FROM "unshackled-word"."VocabRoots" """);
        var ranks = (await connection.QueryAsync<(string Key, int Rank)>("""
            SELECT vs."StrongsKey", e."FrequencyRank" FROM "unshackled-word"."VocabStrongs" vs
                JOIN "unshackled-word"."VocabEntries" e ON e."Id" = vs."VocabEntryId"
            WHERE vs."StrongsKey" IN ('G3056', 'G4901')
            """)).ToDictionary(r => r.Key, r => r.Rank);

        // keep what the dev DB holds for the test keys and put it back afterwards
        var snapshot = (await connection.QueryAsync<dynamic>("""SELECT * FROM "unshackled-word"."VocabStrongsRoots" WHERE "StrongsKey" IN ('G3056', 'G4901')""")).ToList();
        var runIds = new List<int>();

        try
        {
            await connection.ExecuteAsync("""DELETE FROM "unshackled-word"."VocabStrongsRoots" WHERE "StrongsKey" IN ('G3056', 'G4901')""");

            foreach (var rank in ranks.Values)
            {
                var created = await service.CreateRunAsync(new StartVocabRootRunRequest { Course = "nt-greek", FromRank = rank, ToRank = rank, Profile = "stub" }, userId, TestContext.Current.CancellationToken);
                created.Outcome.ShouldBe(AgentRunPrepareOutcome.Ok, created.Error);
                runIds.Add(created.RunId);
                await executor.RunAsync(created.RunId, TestContext.Current.CancellationToken);
            }

            var rows = (await connection.QueryAsync<(string Key, string Origin, int? RunId, string? Prefixes, string? RootKey)>("""
                SELECT s."StrongsKey", s."Origin", s."AgentRunId", s."Prefixes", r."StrongsKey"
                FROM "unshackled-word"."VocabStrongsRoots" s JOIN "unshackled-word"."VocabRoots" r ON r."Id" = s."VocabRootId"
                WHERE s."StrongsKey" IN ('G3056', 'G4901')
                """)).ToDictionary(r => r.Key);

            var first = (await runs.GetRunAsync(runIds[0], TestContext.Current.CancellationToken))!;
            rows.ContainsKey("G3056").ShouldBeTrue($"{first.Status}: {first.StatusNote} (ranks {string.Join(",", ranks.Values)}, calls {stub.Calls})");
            rows["G3056"].Origin.ShouldBe("agent");
            rows["G3056"].RootKey.ShouldBe("G3056");
            rows["G4901"].Prefixes.ShouldBe("σύν + ἐπί");
            rows["G4901"].RootKey.ShouldBe("G3140");
            runIds.ShouldContain(rows["G4901"].RunId!.Value);
            stub.Calls.ShouldBe(3); // one call for λόγος, two for the compound (invented key, then the retry)

            var run = await runs.GetRunAsync(runIds[0], TestContext.Current.CancellationToken);
            run!.Mode.ShouldBe(AgentModes.VocabRoot);
            run.Status.ShouldBe(AgentRunStatuses.Completed);
            run.InputTokens.ShouldBeGreaterThan(0);

            // the run report: rejected before flagged before replaced, with the lemma for the editor link
            var entryId = await connection.QuerySingleAsync<int>("""SELECT "VocabEntryId" FROM "unshackled-word"."VocabStrongs" WHERE "StrongsKey" = 'G3056'""");
            await roots.SetItemAsync(runIds[0], entryId, "G3056", "flagged", "does not concatenate", "ἐκ + G3056", TestContext.Current.CancellationToken);
            var report = await roots.GetReportAsync(runIds[0], TestContext.Current.CancellationToken);
            report.Flagged.ShouldBe(1);
            report.Items.Single().EntryId.ShouldBe(entryId);
            report.Items.Single().Lemma.ShouldNotBeNullOrEmpty();

            // a second pass has nothing left to ask
            (await service.EstimateAsync(new VocabRootRunRangeRequest { Course = "nt-greek", FromRank = ranks["G3056"], ToRank = ranks["G3056"], Profile = "stub" }, null, null, TestContext.Current.CancellationToken))
                .Outcome.ShouldBe(AgentRunPrepareOutcome.NotFound);
        }
        finally
        {
            await connection.ExecuteAsync("""DELETE FROM "unshackled-word"."VocabStrongsRoots" WHERE "StrongsKey" IN ('G3056', 'G4901')""");

            foreach (var row in snapshot)
            {
                await connection.ExecuteAsync("""
                    INSERT INTO "unshackled-word"."VocabStrongsRoots" ("Lang", "StrongsKey", "VocabRootId", "Prefixes", "PrefixKeys", "Origin", "AgentRunId", "UpdatedAt", "UpdatedByUserId")
                    VALUES (@Lang, @StrongsKey, @VocabRootId, @Prefixes, @PrefixKeys, @Origin, @AgentRunId, @UpdatedAt, @UpdatedByUserId)
                    """, new { row.Lang, row.StrongsKey, row.VocabRootId, row.Prefixes, row.PrefixKeys, row.Origin, row.AgentRunId, row.UpdatedAt, row.UpdatedByUserId });
            }

            await connection.ExecuteAsync("""DELETE FROM "unshackled-word"."VocabRoots" WHERE "Id" > @Max AND "ParentRootId" IS NULL""", new { Max = maxRootId });

            foreach (var id in runIds)
            {
                await connection.ExecuteAsync("""DELETE FROM "unshackled-word"."AgentRuns" WHERE "Id" = @Id""", new { Id = id });
            }
        }
    }
}
