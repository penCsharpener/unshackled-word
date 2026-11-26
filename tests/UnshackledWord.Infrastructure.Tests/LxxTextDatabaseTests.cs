using Dapper;
using Npgsql;
using UnshackledWord.Application.Features.Lxx;
using UnshackledWord.Domain.Models.Dbo.Lxx;
using UnshackledWord.Infrastructure.Lxx;

namespace UnshackledWord.Infrastructure.Tests;

/// <summary>
/// Roadmap 0028.3 against a real Postgres with migration 0050: the script runs twice, and the import writes verses and words with their verse
/// ids (unnest arrays incl. NULLs) only while <c>LxxVerses</c> is empty. One rolled-back transaction per test; existing rows are never deleted,
/// so on a database that already holds the LXX text the insert test checks the gate instead. Skipped without <c>UNSHACKLEDWORD_TEST_POSTGRES</c>.
/// </summary>
public class LxxTextDatabaseTests
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

    [Fact]
    public async Task Migration0050_RunsTwice() => await WithTransactionAsync(async (connection, transaction) =>
    {
        var script = await File.ReadAllTextAsync(Path.Combine(SolutionRoot(), "src", "UnshackledWord.Tooling.Postgres.Migration", "scripts", "0050_Lxx.sql"),
            TestContext.Current.CancellationToken);

        await connection.ExecuteAsync(script, null, transaction);
        await connection.ExecuteAsync(script, null, transaction);

        (await connection.ExecuteScalarAsync<int>("""
            SELECT count(*)
            FROM information_schema.tables
            WHERE table_schema = 'unshackled-word'
                AND table_name IN ('LxxVerses', 'LxxWords')
            """, null, transaction)).ShouldBe(2);
    });

    private static LxxVerseText Verse(int sortOrder, long? rahlfsRefId, string kind, int? heb, int? after, params string[] words) => new(
        new LxxVerseDbo
        {
            BibleBookId = 17,
            Chapter = 1,
            Verse = 1,
            Subverse = rahlfsRefId is { } r ? (int)(r % 100) : 0,
            RahlfsRefId = rahlfsRefId,
            SortOrder = sortOrder,
            SweteRef = "Est.1:1",
            LxxRefId = heb,
            HebRefId = heb,
            AfterHebRefId = after,
            MapKind = kind,
        },
        words.Select((w, i) => new LxxWordDbo { Position = i + 1, Greek = w, GreekNoDiacritics = w, SrcHash = "0123abcd" }).ToList());

    [Fact]
    public async Task Insert_WritesVersesAndWords_OnlyWhileEmpty() => await WithTransactionAsync(async (connection, transaction) =>
    {
        var token = TestContext.Current.CancellationToken;
        var verses = new[]
        {
            Verse(900_001, 1_700_100_101L, LxxMapKinds.Addition, null, 17_001_001, "ΕΤΟΥΣ", "δευτέρου"),
            Verse(900_002, 1_700_100_100L, LxxMapKinds.One, 17_001_001, null, "Καὶ", "ἐγένετο", "μετὰ"),
        };

        var empty = await connection.ExecuteScalarAsync<bool>("""SELECT NOT EXISTS (SELECT 1 FROM "unshackled-word"."LxxVerses")""", null, transaction);
        var inserted = await LxxTextRepository.InsertAsync(connection, transaction, verses, token);
        if (!empty)
        {
            inserted.ShouldBe(0);
            return;
        }

        inserted.ShouldBe(2);
        var rows = (await connection.QueryAsync<(int SortOrder, long RahlfsRefId, int? HebRefId, int? AfterHebRefId, long Words, int MaxPosition)>("""
            SELECT v."SortOrder", v."RahlfsRefId", v."HebRefId", v."AfterHebRefId", count(w."Id"), max(w."Position")
            FROM "unshackled-word"."LxxVerses" v
                JOIN "unshackled-word"."LxxWords" w ON w."LxxVerseId" = v."Id"
            GROUP BY v."Id"
            ORDER BY v."SortOrder"
            """, null, transaction)).ToList();

        rows.Count.ShouldBe(2);
        rows[0].RahlfsRefId.ShouldBe(1_700_100_101L);
        rows[0].HebRefId.ShouldBeNull();
        rows[0].AfterHebRefId.ShouldBe(17_001_001);
        rows[0].Words.ShouldBe(2);
        rows[1].HebRefId.ShouldBe(17_001_001);
        rows[1].MaxPosition.ShouldBe(3);
        verses.SelectMany(v => v.Words).ShouldAllBe(w => w.LxxVerseId > 0);

        (await LxxTextRepository.InsertAsync(connection, transaction, verses, token)).ShouldBe(0);
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
