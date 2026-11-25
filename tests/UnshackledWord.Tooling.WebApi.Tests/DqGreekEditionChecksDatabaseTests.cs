using System.Data;
using Dapper;
using Npgsql;
using UnshackledWord.Tooling.WebApi.Endpoints.DataQuality;

namespace UnshackledWord.Tooling.WebApi.Tests;

/// <summary>
/// The Greek edition checks G1/G2 (roadmap 0025.3) against a real Postgres with the STEP Greek words imported and refreshed. Every test runs in one
/// rolled-back transaction (the refresh function truncates inside it). Skipped without <c>UNSHACKLEDWORD_TEST_POSTGRES</c>.
/// </summary>
public class DqGreekEditionChecksDatabaseTests
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

    private static async Task<(int Id, int RefId)> TakeWordAsync(IDbConnection c, IDbTransaction t, string editions)
    {
        var word = await c.QuerySingleAsync<(int Id, int RefId)>("""
            SELECT "Id", "LxxRefId" FROM "unshackled-word"."StepGreekWords" ORDER BY "LxxRefId", "PositionInVerse" LIMIT 1
            """, transaction: t);
        await c.ExecuteAsync("""
            UPDATE "unshackled-word"."StepGreekWords" SET "Editions" = @editions WHERE "Id" = @Id
            """, new { editions, word.Id }, t);
        await c.ExecuteAsync("""SELECT "unshackled-word"."RefreshStepGreekWordEditions"()""", transaction: t);
        return word;
    }

    [Fact]
    public Task UnknownEdition_IsFoundWithWordCountAndVerse() => WithTransactionAsync(async (c, t) =>
    {
        var word = await TakeWordAsync(c, t, "NA28+XYZ");

        var (total, items) = await DqGreekEditionChecks.FindingsAsync(c, t, "G1", All, 1, 50, TestContext.Current.CancellationToken);

        total.ShouldBe(1);
        var finding = items.ShouldHaveSingleItem();
        finding.Check.ShouldBe("G1");
        finding.German.ShouldBe("XYZ");
        (finding.BibleBookId, finding.Chapter, finding.Verse).ShouldBe((word.RefId / 1_000_000, word.RefId / 1_000 % 1_000, word.RefId % 1_000));
        finding.Detail.ShouldContain("XYZ");
        finding.Detail.ShouldContain("1 Wörter");
        (await DqGreekEditionChecks.CountAsync(c, t, "G1", All, TestContext.Current.CancellationToken)).Count.ShouldBe(1);
        (await DqGreekEditionChecks.CountAsync(c, t, "G2", All, TestContext.Current.CancellationToken)).Count.ShouldBe(0);
    });

    [Fact]
    public Task NonEmptyEditionsWithoutToken_IsReportedAsWordWithoutEditions() => WithTransactionAsync(async (c, t) =>
    {
        var word = await TakeWordAsync(c, t, "+");

        var (total, items) = await DqGreekEditionChecks.FindingsAsync(c, t, "G2", All, 1, 50, TestContext.Current.CancellationToken);

        total.ShouldBe(1);
        var finding = items.ShouldHaveSingleItem();
        finding.Severity.ShouldBe("error");
        finding.StepWordId.ShouldBe(word.Id);
        (await DqGreekEditionChecks.CountAsync(c, t, "G2", All, TestContext.Current.CancellationToken)).ShouldBe(new CheckCount(1, 1, null));
    });

    [Fact]
    public Task CleanData_HasNoFindings() => WithTransactionAsync(async (c, t) =>
    {
        foreach (var code in DqGreekEditionChecks.Codes)
        {
            (await DqGreekEditionChecks.FindingsAsync(c, t, code, All, 1, 50, TestContext.Current.CancellationToken)).Total.ShouldBe(0);
        }
    });
}
