using Dapper;
using Npgsql;
using UnshackledWord.Application.Features.Lxx;
using UnshackledWord.Infrastructure.Lxx;

namespace UnshackledWord.Infrastructure.Tests;

/// <summary>
/// Roadmap 0028.5 against a real Postgres with migration 0051: the script runs twice, the CHECKs refuse bad rows, and the read SQL of
/// <see cref="LxxReadRepository"/> returns tags with their TBESG gloss. One rolled-back transaction per test; the tag tests need the LXX text
/// (Gen 1:1) and are skipped without it. Skipped without <c>UNSHACKLEDWORD_TEST_POSTGRES</c>.
/// </summary>
public class LxxTagsDatabaseTests
{
    private const string EnvVar = "UNSHACKLEDWORD_TEST_POSTGRES";
    private const int Gen11 = 1_001_001;

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

    private static string SolutionRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "UnshackledWord.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Solution root not found.");
    }

    /// <summary>The ids of the first words of Gen 1:1 (ἐν, ἀρχῇ, ἐποίησεν …); skips the test when the LXX text is not imported.</summary>
    private static async Task<int[]> Gen11WordIdsAsync(NpgsqlConnection c, NpgsqlTransaction t)
    {
        var ids = (await c.QueryAsync<int>("""
            SELECT w."Id"
            FROM "unshackled-word"."LxxVerses" l
            JOIN "unshackled-word"."LxxWords" w ON w."LxxVerseId" = l."Id"
            WHERE 1=1
                AND l."HebRefId" = @Gen11
            ORDER BY l."SortOrder", w."Position"
            """, new { Gen11 }, t)).ToArray();
        Assert.SkipWhen(ids.Length < 3, "The LXX text (Gen 1:1) is not imported.");
        return ids;
    }

    private static Task InsertTagAsync(NpgsqlConnection c, NpgsqlTransaction t, int wordId, string lemma, string strongs, string morphology, string status,
        string source = "human") =>
        c.ExecuteAsync("""
            INSERT INTO "unshackled-word"."LxxWordTags" ("LxxWordId", "Lemma", "Strongs", "Morphology", "Status", "Source")
            VALUES (@WordId, @Lemma, @Strongs, @Morphology, @Status, @Source)
            """, new { WordId = wordId, Lemma = lemma, Strongs = strongs, Morphology = morphology, Status = status, Source = source }, t);

    [Fact]
    public async Task Migration0051_RunsTwice() => await WithTransactionAsync(async (connection, transaction) =>
    {
        var script = await File.ReadAllTextAsync(Path.Combine(SolutionRoot(), "src", "UnshackledWord.Tooling.Postgres.Migration", "scripts", "0051_LxxTags.sql"),
            TestContext.Current.CancellationToken);

        await connection.ExecuteAsync(script, null, transaction);
        await connection.ExecuteAsync(script, null, transaction);

        (await connection.ExecuteScalarAsync<int>("""
            SELECT count(*)
            FROM information_schema.tables
            WHERE table_schema = 'unshackled-word'
                AND table_name IN ('LxxWordTags', 'LxxHebrewLinks', 'LxxTagAudit')
            """, null, transaction)).ShouldBe(3);
        (await connection.ExecuteScalarAsync<string>("""
            SELECT pg_get_constraintdef(c.oid)
            FROM pg_constraint c
            WHERE c.conname = 'AgentVerseProposals_Language_CK'
            """, null, transaction)).ShouldContain("'lxx'");
    });

    [Theory]
    [InlineData("G0746", "N-DSF", "rejected", "human")]
    [InlineData("G0746", "N-DSF", "confirmed", "seed")]
    [InlineData("H7225", "N-DSF", "confirmed", "human")]
    [InlineData("G746", "N-DSF", "confirmed", "human")]
    [InlineData("G0746", " ", "confirmed", "human")]
    public async Task Checks_RefuseBadTags(string strongs, string morphology, string status, string source) =>
        await WithTransactionAsync(async (connection, transaction) =>
        {
            var ids = await Gen11WordIdsAsync(connection, transaction);
            await connection.ExecuteAsync("SAVEPOINT bad", null, transaction);

            await Should.ThrowAsync<PostgresException>(() => InsertTagAsync(connection, transaction, ids[1], "ἀρχή", strongs, morphology, status, source));

            await connection.ExecuteAsync("ROLLBACK TO SAVEPOINT bad", null, transaction);
        });

    [Fact]
    public async Task ReadSql_ReturnsTagsWithGloss_AnyStatus() => await WithTransactionAsync(async (connection, transaction) =>
    {
        var ids = await Gen11WordIdsAsync(connection, transaction);
        await connection.ExecuteAsync("""DELETE FROM "unshackled-word"."LxxWordTags" WHERE "LxxWordId" = ANY(@Ids)""", new { Ids = ids }, transaction);
        await InsertTagAsync(connection, transaction, ids[1], "ἀρχή", "G0746", "N-DSF", LxxTagVisibility.Confirmed);
        await InsertTagAsync(connection, transaction, ids[2], "ποιέω", "G4160", "V-AAI-3S", LxxTagVisibility.Proposed, "ai");

        using var grid = await connection.QueryMultipleAsync(LxxReadSql.Chapter, new { FromHebRefId = Gen11, ToHebRefId = Gen11 + 30 }, transaction);
        _ = await grid.ReadAsync<int>();
        _ = await grid.ReadAsync<LxxUnitRow>();
        var words = (await grid.ReadAsync<LxxWordRow>()).ToDictionary(w => w.Id);

        words[ids[0]].Status.ShouldBeNull();
        words[ids[0]].Gloss.ShouldBeNull();
        words[ids[1]].Status.ShouldBe(LxxTagVisibility.Confirmed);
        words[ids[1]].Strongs.ShouldBe("G0746");
        words[ids[1]].Gloss.ShouldBe("beginning");
        words[ids[2]].Status.ShouldBe(LxxTagVisibility.Proposed);
        words[ids[2]].Source.ShouldBe("ai");

        var word = await connection.QuerySingleAsync<LxxWordDetailRow>(LxxReadSql.Word, new { LxxWordId = ids[1] }, transaction);
        word.Position.ShouldBe(2); // Swete prints the opening words in capitals: ΕΝ ΑΡΧΗ
        word.Morphology.ShouldBe("N-DSF");
        word.BibleBookId.ShouldBe(1);
        word.HebRefId.ShouldBe(Gen11);
        LxxWordReadService.Build(word, includeProposed: false, lexicon: null).Tag!.ParseGerman.ShouldBe("Nomen Dativ Singular feminin");
    });
}
