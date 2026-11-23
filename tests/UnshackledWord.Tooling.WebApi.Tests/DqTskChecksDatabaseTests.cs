using System.Data;
using Dapper;
using Npgsql;
using UnshackledWord.Tooling.WebApi.Endpoints.DataQuality;

namespace UnshackledWord.Tooling.WebApi.Tests;

/// <summary>
/// The TSK curation checks T1/T2 (roadmap 0024 follow-up) against a real Postgres with seeded TSK and Elb1871. Every test runs in one rolled-back
/// transaction; the curation tables stay untouched. Skipped without <c>UNSHACKLEDWORD_TEST_POSTGRES</c>.
/// </summary>
public class DqTskChecksDatabaseTests
{
    private const string EnvVar = "UNSHACKLEDWORD_TEST_POSTGRES";
    private const int Gen11 = 1_001_001;
    private const int Joh11 = 43_001_001;
    private const int Joh13 = 43_001_003;

    /// <summary>Rev 22,21 is not a TSK target of 1Mo 1,1.</summary>
    private const int Rev2221 = 66_022_021;

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

    private static Task InsertAsync(IDbConnection c, IDbTransaction t, string origin, int target, int? targetEnd, int? start = null, int? end = null, string? quote = null) =>
        c.ExecuteAsync("""
            INSERT INTO "unshackled-word"."TskCurations" ("SourceHebRefId", "TargetStartHebRefId", "TargetEndHebRefId", "Origin", "Rating", "StartPosition", "EndPosition", "QuotedText")
            VALUES (@Gen11, @target, @targetEnd, @origin, 'highlight', @start, @end, @quote)
            """, new { Gen11, target, targetEnd, origin, start, end, quote }, t);

    private static Task<string> WordAsync(IDbConnection c, IDbTransaction t, int position) =>
        c.QuerySingleAsync<string>("""
            SELECT "WordInContext" FROM "unshackled-word"."Elb1871Words" WHERE "HebRefId" = @Gen11 AND "PositionInVerse" = @position
            """, new { Gen11, position }, t);

    private static async Task<List<UnshackledWord.Domain.WebApi.DataQuality.Finding>> FindingsAsync(IDbConnection c, IDbTransaction t, string code) =>
        (await DqTskChecks.FindingsAsync(c, t, code, All, 1, 250, TestContext.Current.CancellationToken)).Items;

    [Fact]
    public Task OrphanTskCuration_IsFound_AndHealthyOnesAreNot() => WithTransactionAsync(async (c, t) =>
    {
        await InsertAsync(c, t, "tsk", Joh11, Joh13);
        await InsertAsync(c, t, "tsk", Rev2221, null);

        var items = await FindingsAsync(c, t, "T1");

        var orphan = items.ShouldHaveSingleItem();
        orphan.Check.ShouldBe("T1");
        orphan.Severity.ShouldBe("error");
        (orphan.BibleBookId, orphan.Chapter, orphan.Verse).ShouldBe((1, 1, 1));
        (orphan.TargetBibleBookId, orphan.TargetChapter, orphan.TargetVerse, orphan.TargetEndVerse).ShouldBe((66, 22, 21, null));

        var count = await DqTskChecks.CountAsync(c, t, "T1", All, TestContext.Current.CancellationToken);
        (count.Count, count.ErrorCount, count.Sub!.Count).ShouldBe((1, 1, 0));
    });

    [Fact]
    public Task AddedCuration_WithoutSourceVerse_IsFound_ButAnAddedLinkOnAnExistingVerseIsNot() => WithTransactionAsync(async (c, t) =>
    {
        await InsertAsync(c, t, "added", Rev2221, null);
        await c.ExecuteAsync("""
            INSERT INTO "unshackled-word"."TskCurations" ("SourceHebRefId", "TargetStartHebRefId", "Origin") VALUES (66022999, @Joh11, 'added')
            """, new { Joh11 }, t);

        var items = await FindingsAsync(c, t, "T1");

        var gone = items.ShouldHaveSingleItem();
        (gone.BibleBookId, gone.Chapter, gone.Verse).ShouldBe((66, 22, 999));
        (await DqTskChecks.CountAsync(c, t, "T1", All, TestContext.Current.CancellationToken)).Sub!.Count.ShouldBe(1);
    });

    [Fact]
    public Task DriftedWordLink_IsFound_AfterTheWordChanged() => WithTransactionAsync(async (c, t) =>
    {
        var quote = await WordAsync(c, t, 2);
        await InsertAsync(c, t, "tsk", Joh11, Joh13, 2, 2, quote);
        await c.ExecuteAsync("""
            UPDATE "unshackled-word"."Elb1871Words" SET "WordInContext" = 'Zzzgeaendert' WHERE "HebRefId" = @Gen11 AND "PositionInVerse" = 2
            """, new { Gen11 }, t);

        var drifted = (await FindingsAsync(c, t, "T2")).ShouldHaveSingleItem();

        drifted.Severity.ShouldBe("warning");
        drifted.QuotedText.ShouldBe(quote);
        drifted.CurrentText.ShouldBe("Zzzgeaendert");
        (drifted.StartPosition, drifted.EndPosition).ShouldBe((2, 2));
        (drifted.TargetBibleBookId, drifted.TargetChapter, drifted.TargetVerse, drifted.TargetEndVerse).ShouldBe((43, 1, 1, 3));
        (await DqTskChecks.CountAsync(c, t, "T2", All, TestContext.Current.CancellationToken)).ShouldBe(new CheckCount(1, 0, null));
        (await FindingsAsync(c, t, "T1")).ShouldBeEmpty();
    });

    [Fact]
    public Task WordLinkWithGonePositions_IsFound() => WithTransactionAsync(async (c, t) =>
    {
        await InsertAsync(c, t, "tsk", Joh11, Joh13, 50, 51, "Nirgendwo");

        var gone = (await FindingsAsync(c, t, "T2")).ShouldHaveSingleItem();

        gone.CurrentText.ShouldBeNull();
        gone.Detail.ShouldContain("nicht mehr vorhanden");
    });

    [Fact]
    public Task HealthyWordLink_IsNotFound_AndPunctuationDoesNotCount() => WithTransactionAsync(async (c, t) =>
    {
        var quote = await WordAsync(c, t, 2);
        await InsertAsync(c, t, "tsk", Joh11, Joh13, 2, 2, quote + ",");

        (await FindingsAsync(c, t, "T2")).ShouldBeEmpty();
        (await FindingsAsync(c, t, "T1")).ShouldBeEmpty();
    });

    [Fact]
    public Task Facets_FilterBySourceBook() => WithTransactionAsync(async (c, t) =>
    {
        await InsertAsync(c, t, "tsk", Rev2221, null);

        var other = await DqTskChecks.CountAsync(c, t, "T1", new DqFacets(null, 43), TestContext.Current.CancellationToken);
        var nt = await DqTskChecks.CountAsync(c, t, "T1", new DqFacets("nt", null), TestContext.Current.CancellationToken);
        var ot = await DqTskChecks.CountAsync(c, t, "T1", new DqFacets("ot", null), TestContext.Current.CancellationToken);
        var book = await DqTskChecks.CountAsync(c, t, "T1", new DqFacets(null, 1), TestContext.Current.CancellationToken);

        (other.Count, nt.Count, ot.Count, book.Count).ShouldBe((0, 0, 1, 1));
    });

    [Fact]
    public void TskChecks_AreListedInTheOverview()
    {
        DqChecks.IsKnown("T1").ShouldBeTrue();
        DqChecks.IsKnown("T2").ShouldBeTrue();
    }
}
