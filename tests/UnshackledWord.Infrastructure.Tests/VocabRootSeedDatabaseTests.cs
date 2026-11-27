using System.Data;
using Dapper;
using Npgsql;
using UnshackledWord.Infrastructure.Seed.Vocab;

namespace UnshackledWord.Infrastructure.Tests;

/// <summary>
/// Roadmap 0030.7 (roots.csv and word-roots.csv) against a real Postgres; every test runs in one transaction that is rolled back.
/// Set <c>UNSHACKLEDWORD_TEST_POSTGRES</c>, otherwise the tests are skipped.
/// </summary>
[Collection("VocabDatabase")]
public class VocabRootSeedDatabaseTests
{
    private const string EnvVar = "UNSHACKLEDWORD_TEST_POSTGRES";

    private static async Task WithTransactionAsync(Func<IDbConnection, IDbTransaction, Task> test)
    {
        var connectionString = Environment.GetEnvironmentVariable(EnvVar);
        Assert.SkipWhen(string.IsNullOrWhiteSpace(connectionString), $"{EnvVar} is not set.");

        using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction();

        await test(connection, transaction);

        transaction.Rollback();
    }

    private static Task<int> RootAsync(IDbConnection c, IDbTransaction t, string root, string key, int? parent = null) =>
        c.QuerySingleAsync<int>("""
            INSERT INTO "unshackled-word"."VocabRoots" ("Track", "Root", "RootNormalized", "Homograph", "StrongsKey", "ParentRootId")
            VALUES ('G', @root, @root, 1, @key, @parent)
            RETURNING "Id"
            """, new { root, key, parent }, t);

    private static Task WordAsync(IDbConnection c, IDbTransaction t, string key, int rootId, string origin, string? prefixes = null, string[]? prefixKeys = null) =>
        c.ExecuteAsync("""
            INSERT INTO "unshackled-word"."VocabStrongsRoots" ("Lang", "StrongsKey", "VocabRootId", "Prefixes", "PrefixKeys", "Origin")
            VALUES ('G', @key, @rootId, @prefixes, @prefixKeys::varchar(10)[], @origin)
            """, new { key, rootId, prefixes, prefixKeys, origin }, t);

    private static async Task ClearAsync(IDbConnection c, IDbTransaction t)
    {
        await c.ExecuteAsync("""DELETE FROM "unshackled-word"."VocabStrongsRoots" """, transaction: t);
        await c.ExecuteAsync("""UPDATE "unshackled-word"."VocabRoots" SET "ParentRootId" = NULL""", transaction: t);
        await c.ExecuteAsync("""DELETE FROM "unshackled-word"."VocabRoots" """, transaction: t);
    }

    [Fact]
    public async Task RoundTrip_ExportClearImport_GivesIdenticalRootsAndParents() => await WithTransactionAsync(async (c, t) =>
    {
        var parent = await RootAsync(c, t, "τεστα", "G99001");
        var child = await RootAsync(c, t, "τεστβ", "G99002", parent);
        await WordAsync(c, t, "G99001", parent, "prefill");
        await WordAsync(c, t, "G99002", child, "agent");
        await WordAsync(c, t, "G99003", child, "manual", "σύν + ἐπί", ["G4862", "G1909"]);

        var first = VocabSeedExporter.Serialize(await VocabSeedExporter.ReadAsync(c, t));
        var roots = VocabSeedExporter.Serialize(await VocabSeedExporter.ReadAsync(c, t)).Single(f => f.Name == VocabSeedExporter.RootsFile).Bytes;
        System.Text.Encoding.UTF8.GetString(roots).ShouldContain("τεστβ,1,G99002,τεστα,1");

        await ClearAsync(c, t);
        var data = VocabSeedImporter.Parse(first[0].Bytes, first[1].Bytes, first[2].Bytes, first[3].Bytes, first[4].Bytes);
        var report = await VocabSeedImporter.ImportAsync(c, t, data);
        report.Counts["rootsNew"].ShouldBe(data.Roots.Count);
        report.Counts["wordRootsNew"].ShouldBe(data.WordRoots.Count);

        var second = VocabSeedExporter.Serialize(await VocabSeedExporter.ReadAsync(c, t));
        second[3].Bytes.ShouldBe(first[3].Bytes, "roots.csv differs after the round trip");
        second[4].Bytes.ShouldBe(first[4].Bytes, "word-roots.csv differs after the round trip");

        // a second import changes nothing
        var again = await VocabSeedImporter.ImportAsync(c, t, data);
        again.Counts["rootsNew"].ShouldBe(0);
        again.Counts["rootsChanged"].ShouldBe(0);
        again.Counts["wordRootsNew"].ShouldBe(0);
        again.Counts["wordRootsChanged"].ShouldBe(0);
    });

    [Fact]
    public async Task Import_KeepsALocalManualRow_AgainstASeedAgentRow() => await WithTransactionAsync(async (c, t) =>
    {
        await ClearAsync(c, t);
        var local = await RootAsync(c, t, "τεστα", "G99001");
        await WordAsync(c, t, "G99010", local, "manual");
        await WordAsync(c, t, "G99011", local, "agent");

        var data = new VocabSeedData
        {
            Roots =
            [
                new VocabSeedRootLine { Track = "G", Root = "τεστα", Homograph = 1, StrongsKey = "G99001" },
                new VocabSeedRootLine { Track = "G", Root = "τεστβ", Homograph = 1, StrongsKey = "G99002", ParentRoot = "τεστα", ParentHomograph = 1 }
            ],
            WordRoots =
            [
                new VocabSeedWordRootLine { Lang = "G", StrongsKey = "G99010", RootTrack = "G", Root = "τεστβ", RootHomograph = 1, Origin = "agent" },
                new VocabSeedWordRootLine { Lang = "G", StrongsKey = "G99011", RootTrack = "G", Root = "τεστβ", RootHomograph = 1, Origin = "manual" }
            ]
        };

        var preview = await VocabRootSeedSync.ApplyAsync(c, t, data);
        preview.RootsNew.ShouldBe(1);
        preview.RootsChanged.ShouldBe(1); // the new root's parent
        preview.WordRootsChanged.ShouldBe(1);
        preview.ManualKept.ShouldBe(1);

        var rows = (await c.QueryAsync<(string Key, string Root, string Origin)>("""
            SELECT s."StrongsKey", r."Root", s."Origin"
            FROM "unshackled-word"."VocabStrongsRoots" s
                JOIN "unshackled-word"."VocabRoots" r ON r."Id" = s."VocabRootId"
            ORDER BY s."StrongsKey"
            """, transaction: t)).ToList();
        rows.ShouldBe([("G99010", "τεστα", "manual"), ("G99011", "τεστβ", "manual")]);
    });
}
