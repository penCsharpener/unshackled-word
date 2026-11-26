using Dapper;
using Npgsql;
using UnshackledWord.Infrastructure.Lxx;

namespace UnshackledWord.Infrastructure.Tests;

/// <summary>
/// Roadmap 0028.6 against a real Postgres with the LXX text: the upsert and stale-delete SQL of pass 1 inside one rolled-back transaction. The
/// proposals already in the dev database are removed inside the transaction first. Skipped without <c>UNSHACKLEDWORD_TEST_POSTGRES</c> or without
/// LXX words.
/// </summary>
public class LxxNtFormDatabaseTests
{
    private const string EnvVar = "UNSHACKLEDWORD_TEST_POSTGRES";

    private static async Task WithTransactionAsync(Func<NpgsqlConnection, NpgsqlTransaction, int[], Task> test)
    {
        var connectionString = Environment.GetEnvironmentVariable(EnvVar);
        Assert.SkipWhen(string.IsNullOrWhiteSpace(connectionString), $"{EnvVar} is not set.");

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken);

        var ids = (await connection.QueryAsync<int>("""SELECT w."Id" FROM "unshackled-word"."LxxWords" w ORDER BY w."Id" LIMIT 4""", null, transaction)).ToArray();
        Assert.SkipWhen(ids.Length < 4, "The LXX text is not imported.");
        await connection.ExecuteAsync("""DELETE FROM "unshackled-word"."LxxWordTags" WHERE "Source" = 'nt-form' OR "LxxWordId" = ANY(@Ids)""", new { Ids = ids }, transaction);

        await test(connection, transaction, ids);

        await transaction.RollbackAsync(TestContext.Current.CancellationToken);
    }

    private static object Parameters(int[] ids, string lemma = "λόγος") => new
    {
        Ids = ids,
        Lemmas = ids.Select(_ => lemma).ToArray(),
        Strongs = ids.Select(_ => "G3056").ToArray(),
        Morphologies = ids.Select(_ => "N-GSM").ToArray(),
    };

    [Fact]
    public async Task Upsert_IsIdempotent_AndNeverTouchesConfirmedOrAi() => await WithTransactionAsync(async (c, t, ids) =>
    {
        await c.ExecuteAsync("""
            INSERT INTO "unshackled-word"."LxxWordTags" ("LxxWordId", "Lemma", "Strongs", "Morphology", "Status", "Source")
            VALUES (@A, 'x', 'G0001', 'N-NSM', 'confirmed', 'human'), (@B, 'y', 'G0002', 'N-NSM', 'proposed', 'ai')
            """, new { A = ids[0], B = ids[1] }, t);

        var first = (await c.QueryAsync<bool>(LxxNtFormSql.Upsert, Parameters(ids), t)).ToList();
        first.ShouldBe([true, true]);           // only the two free words are inserted

        (await c.QueryAsync<bool>(LxxNtFormSql.Upsert, Parameters(ids), t)).ShouldBeEmpty();   // second run: 0 rows

        var kept = (await c.QueryAsync<(string Lemma, string Status, string Source, int? UserId)>("""
            SELECT t."Lemma", t."Status", t."Source", t."UserId" FROM "unshackled-word"."LxxWordTags" t WHERE t."LxxWordId" = ANY(@Ids) ORDER BY t."LxxWordId"
            """, new { Ids = ids }, t)).ToList();
        kept.ShouldBe([("x", "confirmed", "human", null), ("y", "proposed", "ai", null), ("λόγος", "proposed", "nt-form", null), ("λόγος", "proposed", "nt-form", null)]);
    });

    [Fact]
    public async Task Upsert_UpdatesChangedProposals_AndDeleteStaleRemovesOnlyOwnProposals() => await WithTransactionAsync(async (c, t, ids) =>
    {
        await c.ExecuteAsync("""
            INSERT INTO "unshackled-word"."LxxWordTags" ("LxxWordId", "Lemma", "Strongs", "Morphology", "Status", "Source")
            VALUES (@A, 'y', 'G0002', 'N-NSM', 'proposed', 'ai')
            """, new { A = ids[0] }, t);
        var free = ids[1..];
        await c.QueryAsync<bool>(LxxNtFormSql.Upsert, Parameters(free), t);

        (await c.QueryAsync<bool>(LxxNtFormSql.Upsert, Parameters(free, "anders"), t)).ShouldBe([false, false, false]);

        var deleted = await c.ExecuteAsync(LxxNtFormSql.DeleteStale, new { Ids = free[..2] }, t);
        deleted.ShouldBe(1);
        (await c.ExecuteScalarAsync<int>("""SELECT count(*) FROM "unshackled-word"."LxxWordTags" t WHERE t."LxxWordId" = ANY(@Ids)""", new { Ids = ids }, t)).ShouldBe(3);
    });
}
