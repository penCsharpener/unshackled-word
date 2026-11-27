using System.Data;
using Dapper;
using Npgsql;
using UnshackledWord.Tooling.WebApi.Endpoints.DataQuality;

namespace UnshackledWord.Tooling.WebApi.Tests;

/// <summary>The vocab root checks V1-V3 (roadmap 0030.7) against a real Postgres, in one rolled-back transaction. Skipped without <c>UNSHACKLEDWORD_TEST_POSTGRES</c>.</summary>
public class DqVocabRootChecksDatabaseTests
{
    private const string EnvVar = "UNSHACKLEDWORD_TEST_POSTGRES";
    private static readonly DqFacets All = new(null, null);

    private static async Task WithTransactionAsync(Func<IDbConnection, IDbTransaction, Task> test)
    {
        var connectionString = Environment.GetEnvironmentVariable(EnvVar);
        Assert.SkipWhen(string.IsNullOrWhiteSpace(connectionString), $"{EnvVar} is not set.");

        using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        using var transaction = connection.BeginTransaction();
        await test(connection, transaction);
        transaction.Rollback();
    }

    private static async Task<int> RootAsync(IDbConnection c, IDbTransaction t, string root, string key) =>
        await c.QuerySingleAsync<int>("""
            INSERT INTO "unshackled-word"."VocabRoots" ("Track", "Root", "RootNormalized", "Homograph", "StrongsKey")
            VALUES ('G', @root, @root, 1, @key)
            RETURNING "Id"
            """, new { root, key }, t);

    private static async Task WordAsync(IDbConnection c, IDbTransaction t, string key, int rootId) =>
        await c.ExecuteAsync("""
            INSERT INTO "unshackled-word"."VocabStrongsRoots" ("Lang", "StrongsKey", "VocabRootId", "Origin")
            VALUES ('G', @key, @rootId, 'manual')
            """, new { key, rootId }, t);

    [Fact]
    public Task KeysWithDifferentRoots_RootKeyMissing_OwnKeyElsewhere() => WithTransactionAsync(async (c, t) =>
    {
        var ct = TestContext.Current.CancellationToken;
        var before = new[] { "V1", "V2", "V3" }.ToDictionary(x => x, x => DqVocabRootChecks.CountAsync(c, t, x, All, ct).Result.Count);

        var a = await RootAsync(c, t, "τεστα", "G99001");
        var b = await RootAsync(c, t, "τεστβ", "G99002");
        await WordAsync(c, t, "G99001", b); // V3: the own key of root a belongs to root b
        await WordAsync(c, t, "G99003", a);
        var entry = await c.QuerySingleAsync<int>("""
            INSERT INTO "unshackled-word"."VocabEntries" ("Track", "LanguageId", "Lemma", "LemmaNormalized", "Occurrences")
            VALUES ('G', 2, 'τεστε', 'τεστε', 1)
            RETURNING "Id"
            """, transaction: t);
        await c.ExecuteAsync("""
            INSERT INTO "unshackled-word"."VocabStrongs" ("VocabEntryId", "Lang", "StrongsKey", "Occurrences")
            VALUES (@entry, 'G', 'G99001', 2), (@entry, 'G', 'G99003', 1)
            """, new { entry }, t);

        (await DqVocabRootChecks.CountAsync(c, t, "V1", All, ct)).Count.ShouldBe(before["V1"] + 1);
        (await DqVocabRootChecks.CountAsync(c, t, "V2", All, ct)).Count.ShouldBe(before["V2"] + 2); // G99001 and G99002 are no lexicon entries
        (await DqVocabRootChecks.CountAsync(c, t, "V3", All, ct)).Count.ShouldBe(before["V3"] + 1);

        var (_, items) = await DqVocabRootChecks.FindingsAsync(c, t, "V1", All, 1, 5000, ct);
        items.ShouldContain(f => f.Original == "τεστε" && f.Detail.Contains("G99001 → τεστβ"));
        DqChecks.IsKnown("V3").ShouldBeTrue();
    });
}
