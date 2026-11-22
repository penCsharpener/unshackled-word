using System.Data;
using Dapper;
using Npgsql;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.WebApi.Vocab;
using UnshackledWord.Infrastructure.Vocab;

namespace UnshackledWord.Infrastructure.Tests;

/// <summary>
/// Roadmap 0022.8b (lexicon changes under active cards) against a real Postgres with the derived NT lexicon (ranks 1–4). Every test runs in one
/// transaction that is rolled back and uses its own new users. Set <c>UNSHACKLEDWORD_TEST_POSTGRES</c>, otherwise the tests are skipped.
/// </summary>
[Collection("VocabDatabase")]
public class VocabChangeDatabaseTests
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

    private static async Task<int> NewUserAsync(IDbConnection c, IDbTransaction t)
    {
        var name = "vocab-change-" + Guid.NewGuid().ToString("N");

        return await c.QuerySingleAsync<int>("""
            INSERT INTO "unshackled-word"."Users" ("Email", "EmailNormalized", "DisplayName")
            VALUES (@Name || '@example.org', @Name || '@example.org', @Name)
            RETURNING "Id"
            """, new { Name = name }, t);
    }

    private static async Task<List<int>> TopEntriesAsync(IDbConnection c, IDbTransaction t) =>
        (await c.QueryAsync<int>("""
            SELECT e."Id" FROM "unshackled-word"."VocabEntries" e WHERE e."Track" = 'G' AND e."FrequencyRank" BETWEEN 1 AND 4 ORDER BY e."FrequencyRank"
            """, transaction: t)).ToList();

    /// <summary>A learned card (state review) that has seen the entry's current content version.</summary>
    private static async Task GiveCardAsync(IDbConnection c, IDbTransaction t, int userId, int entryId, double stability, DateTime? due = null) =>
        await c.ExecuteAsync("""
            INSERT INTO "unshackled-word"."VocabCards" ("UserId", "VocabEntryId", "State", "Stability", "Difficulty", "Due", "Reps", "Lapses", "LastReview", "SeenContentVersion")
            SELECT @UserId, e."Id", 'review', @Stability, 5, @Due, 3, 0, now(), e."ContentVersion"
            FROM "unshackled-word"."VocabEntries" e
            WHERE e."Id" = @EntryId
            """, new { UserId = userId, EntryId = entryId, Stability = stability, Due = due ?? DateTime.UtcNow.AddDays(-1) }, t);

    private static async Task<VocabChangesResponse> ChangesAsync(IDbConnection c, IDbTransaction t, int userId) =>
        (await VocabChangeService.ListAsync(c, t, userId)).Response!;

    private static async Task<int> CardCountAsync(IDbConnection c, IDbTransaction t, int userId) =>
        await c.QuerySingleAsync<int>("""SELECT count(*)::int FROM "unshackled-word"."VocabCards" WHERE "UserId" = @UserId""", new { UserId = userId }, t);

    [Fact]
    public async Task FormEdit_FlagsTheCardWithTheChange_AndKeepClearsIt() => await WithTransactionAsync(async (c, t) =>
    {
        var userId = await NewUserAsync(c, t);
        var entry = (await TopEntriesAsync(c, t))[0];
        await GiveCardAsync(c, t, userId, entry, 12.5);
        (await ChangesAsync(c, t, userId)).Count.ShouldBe(0);

        var edit = await VocabEntryEditor.UpdateEntryAsync(c, t, entry, "ὁ, ἡ, τό (Test)", null, false, null);
        edit.Status.ShouldBe(VocabEditStatus.Ok);

        var list = await ChangesAsync(c, t, userId);
        list.Count.ShouldBe(1);
        var item = list.Items.Single();
        item.EntryId.ShouldBe(entry);
        item.Status.ShouldBe("changed");
        item.Lang.ShouldBe("grc");
        item.Changes.Select(x => x.Kind).ShouldContain("form");
        item.Changes.Single(x => x.Kind == "form").After.ShouldNotBeNull();
        (await VocabChangeService.CountAsync(c, t, userId)).ShouldBe(1);

        var kept = await VocabChangeService.ApplyAsync(c, t, userId, entry, "keep");
        kept.Status.ShouldBe(VocabSessionStatus.Ok);
        kept.Response!.Remaining.ShouldBe(0);
        (await ChangesAsync(c, t, userId)).Count.ShouldBe(0);
        (await c.QuerySingleAsync<double>("""SELECT "Stability" FROM "unshackled-word"."VocabCards" WHERE "UserId" = @UserId""", new { UserId = userId }, t)).ShouldBe(12.5);
    });

    [Fact]
    public async Task FlaggedCard_IsStillScheduled()  => await WithTransactionAsync(async (c, t) =>
    {
        var userId = await NewUserAsync(c, t);
        var entry = (await TopEntriesAsync(c, t))[0];
        await GiveCardAsync(c, t, userId, entry, 3);
        await VocabEntryEditor.UpdateEntryAsync(c, t, entry, "ὁ, ἡ, τό (Test)", null, false, null);

        (await ChangesAsync(c, t, userId)).Count.ShouldBe(1);
        var due = await VocabSessionRepository.GetDueCardsAsync(c, t, userId, "G", DateTime.UtcNow);
        due.Select(x => x.EntryId).ShouldContain(entry);
    });

    [Fact]
    public async Task Reset_DeletesTheCard_KeepsTheLog_AndTheWordComesBackAsNew() => await WithTransactionAsync(async (c, t) =>
    {
        var userId = await NewUserAsync(c, t);
        var entry = (await TopEntriesAsync(c, t))[0];
        await GiveCardAsync(c, t, userId, entry, 20);
        await c.ExecuteAsync("""
            INSERT INTO "unshackled-word"."VocabReviewLog" ("UserId", "VocabEntryId", "QuizType", "IsCorrect")
            VALUES (@UserId, @EntryId, 'multiple_choice', true)
            """, new { UserId = userId, EntryId = entry }, t);
        await VocabEntryEditor.UpdateEntryAsync(c, t, entry, "ὁ, ἡ, τό (Test)", null, false, null);

        var reset = await VocabChangeService.ApplyAsync(c, t, userId, entry, "reset");

        reset.Status.ShouldBe(VocabSessionStatus.Ok);
        reset.Response!.Remaining.ShouldBe(0);
        (await CardCountAsync(c, t, userId)).ShouldBe(0);
        (await c.QuerySingleAsync<int>("""SELECT count(*)::int FROM "unshackled-word"."VocabReviewLog" WHERE "UserId" = @UserId""", new { UserId = userId }, t)).ShouldBe(1);
        (await VocabSessionRepository.GetNewCandidatesAsync(c, t, userId, "G", 10)).ShouldContain(entry);

        (await VocabChangeService.ApplyAsync(c, t, userId, entry, "reset")).Status.ShouldBe(VocabSessionStatus.NotFound);
        (await VocabChangeService.ApplyAsync(c, t, userId, entry, "nope")).Status.ShouldBe(VocabSessionStatus.Invalid);
    });

    [Fact]
    public async Task Merge_MovesCards_TheHigherStabilityWins_AndFlagsTheTarget() => await WithTransactionAsync(async (c, t) =>
    {
        var top = await TopEntriesAsync(c, t);
        var source = top[2];
        var target = top[3];
        var both = await NewUserAsync(c, t);
        var onlySource = await NewUserAsync(c, t);
        var onlyTarget = await NewUserAsync(c, t);
        var sourceLemma = await c.QuerySingleAsync<string>("""SELECT "Lemma" FROM "unshackled-word"."VocabEntries" WHERE "Id" = @Id""", new { Id = source }, t);

        await GiveCardAsync(c, t, both, source, 30);
        await GiveCardAsync(c, t, both, target, 5);
        await GiveCardAsync(c, t, onlySource, source, 8);
        await GiveCardAsync(c, t, onlyTarget, target, 9);
        await c.ExecuteAsync("""
            INSERT INTO "unshackled-word"."VocabReviewLog" ("UserId", "VocabEntryId", "QuizType", "IsCorrect")
            VALUES (@UserId, @EntryId, 'multiple_choice', true)
            """, new { UserId = both, EntryId = source }, t);

        var merge = await VocabEntryEditor.MergeAsync(c, t, source, target, null);
        merge.Status.ShouldBe(VocabEditStatus.Ok);

        var cards = (await c.QueryAsync<(int UserId, int EntryId, double Stability)>(
            """SELECT "UserId", "VocabEntryId", "Stability" FROM "unshackled-word"."VocabCards" WHERE "UserId" = ANY(@Ids) ORDER BY "UserId", "VocabEntryId" """,
            new { Ids = new[] { both, onlySource, onlyTarget } }, t)).ToList();
        cards.Count.ShouldBe(3);
        cards.ShouldAllBe(x => x.EntryId == target);
        cards.Single(x => x.UserId == both).Stability.ShouldBe(30);
        cards.Single(x => x.UserId == onlySource).Stability.ShouldBe(8);
        cards.Single(x => x.UserId == onlyTarget).Stability.ShouldBe(9);
        (await c.QuerySingleAsync<int>("""SELECT count(*)::int FROM "unshackled-word"."VocabReviewLog" WHERE "UserId" = @UserId""", new { UserId = both }, t)).ShouldBe(1);

        foreach (var user in new[] { both, onlySource, onlyTarget })
        {
            var list = await ChangesAsync(c, t, user);
            list.Count.ShouldBe(1);
            var item = list.Items.Single();
            item.EntryId.ShouldBe(target);
            item.Status.ShouldBe("changed");
            var change = item.Changes.Single(x => x.Kind == "merge");
            change.TargetEntryId.ShouldBe(source);
            change.TargetLemma.ShouldBe(sourceLemma);
            change.After!.Value.GetProperty("mergedFrom").GetInt32().ShouldBe(source);
        }
    });

    [Fact]
    public async Task RetiredEntry_IsRemoved_NotQuizzed_AndCannotBeKept() => await WithTransactionAsync(async (c, t) =>
    {
        var userId = await NewUserAsync(c, t);
        var entry = (await TopEntriesAsync(c, t))[1];
        await GiveCardAsync(c, t, userId, entry, 6);
        (await VocabEntryEditor.RetireAsync(c, t, entry, null)).Status.ShouldBe(VocabEditStatus.Ok);

        var item = (await ChangesAsync(c, t, userId)).Items.Single();
        item.Status.ShouldBe("removed");
        item.Changes.Select(x => x.Kind).ShouldContain("retire");
        (await VocabSessionRepository.GetDueCardsAsync(c, t, userId, "G", DateTime.UtcNow)).Select(x => x.EntryId).ShouldNotContain(entry);

        (await VocabChangeService.ApplyAsync(c, t, userId, entry, "keep")).Status.ShouldBe(VocabSessionStatus.Conflict);
        (await VocabChangeService.ApplyAsync(c, t, userId, entry, "reset")).Status.ShouldBe(VocabSessionStatus.Ok);
        (await ChangesAsync(c, t, userId)).Count.ShouldBe(0);
    });

    [Fact]
    public async Task KeepAll_AcceptsChangedCards_AndLeavesRemovedOnes() => await WithTransactionAsync(async (c, t) =>
    {
        var userId = await NewUserAsync(c, t);
        var top = await TopEntriesAsync(c, t);
        foreach (var entry in top.Take(3))
        {
            await GiveCardAsync(c, t, userId, entry, 4);
        }

        await VocabEntryEditor.UpdateEntryAsync(c, t, top[0], "ὁ, ἡ, τό (Test)", null, false, null);
        await VocabEntryEditor.UpdateEntryAsync(c, t, top[1], "καί (Test)", null, false, null);
        await VocabEntryEditor.RetireAsync(c, t, top[2], null);
        (await ChangesAsync(c, t, userId)).Count.ShouldBe(3);

        var all = (await VocabChangeService.KeepAllAsync(c, t, userId)).Response!;

        all.Kept.ShouldBe(2);
        all.Remaining.ShouldBe(1);
        (await ChangesAsync(c, t, userId)).Items.Single().Status.ShouldBe("removed");
    });

    [Fact]
    public async Task CourseNumbers_KnownDueNewAvailableCoverage_ForASignedInUser() => await WithTransactionAsync(async (c, t) =>
    {
        var userId = await NewUserAsync(c, t);
        var top = await TopEntriesAsync(c, t);
        await GiveCardAsync(c, t, userId, top[0], 30);                                  // strong (strength >= 3), due
        await GiveCardAsync(c, t, userId, top[1], 0.5, DateTime.UtcNow.AddDays(5));     // weak, not due
        var baseline = new VocabCourseRepository(new TestDbReader(c, t));
        var anonymous = (await baseline.GetCoursesAsync()).Single(x => x.Track == "G");
        var course = (await baseline.GetCoursesAsync(userId)).Single(x => x.Track == "G");

        anonymous.Known.ShouldBeNull();
        anonymous.Coverage.ShouldBeNull();
        course.Known.ShouldBe(1);
        course.Due.ShouldBe(1);
        course.NewAvailable.ShouldBe((await VocabSessionRepository.GetNewCandidatesAsync(c, t, userId, "G", int.MaxValue)).Count);
        var occurrences = await c.QuerySingleAsync<int>("""SELECT "Occurrences" FROM "unshackled-word"."VocabEntries" WHERE "Id" = @Id""", new { Id = top[0] }, t);
        course.Coverage.ShouldBe(Math.Round(100.0 * occurrences / anonymous.TotalOccurrences, 1));
    });

    /// <summary>Runs the repository's queries on the test's transaction so it sees the fixture rows.</summary>
    private sealed class TestDbReader(IDbConnection connection, IDbTransaction transaction) : IDbReader
    {
        public Task<T?> ReadFirstOrDefaultAsync<T>(string sql, object? param = null) => connection.QueryFirstOrDefaultAsync<T>(sql, param, transaction);

        public async Task<IEnumerable<T>> ReadAsListAsync<T>(string sql, object? param = null) => await connection.QueryAsync<T>(sql, param, transaction);

        public Task<T?> ExecuteScalarAsync<T>(string sql, object? param = null) => connection.ExecuteScalarAsync<T>(sql, param, transaction);

        public Task<List<T>> ReadMultipleAsListAsync<T>(string sql, object? param, Func<IMultiDbReader, Task<List<T>>> mappingFunc, CancellationToken token = default) =>
            throw new NotSupportedException();
    }
}
