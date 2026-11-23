using System.Data;
using System.Text.Json;
using Dapper;
using Npgsql;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Features.CrossReferences;
using UnshackledWord.Application.Features.TskCuration;
using UnshackledWord.Domain.WebApi.Elberfelder.GetChapterCrossReferences;
using UnshackledWord.Domain.WebApi.Tsk;
using UnshackledWord.Infrastructure.Repositories;
using UnshackledWord.Infrastructure.TskCuration;

namespace UnshackledWord.Infrastructure.Tests;

/// <summary>
/// Roadmap 0024.3 against a real Postgres (migration 0045, seeded TSK): <see cref="TskCurationService"/> with <see cref="TskCurationRepository"/> and the
/// curated chapter list of <see cref="ChapterCrossReferenceService"/> over <see cref="TskRepository"/>. Every test runs in one transaction that is
/// rolled back and uses its own new user. Point <c>UNSHACKLEDWORD_TEST_POSTGRES</c> at a COPY of the dev database, otherwise the tests are skipped.
/// </summary>
public class TskCurationDatabaseTests
{
    private const string EnvVar = "UNSHACKLEDWORD_TEST_POSTGRES";
    private const int Gen11 = 1_001_001;
    private const int Gen12 = 1_001_002;
    private const int Ps336 = 19_033_006;

    /// <summary>KJV Nahum 2,10 („empty, and void“) = Hebrew/Elberfelder Nah 2,11.</summary>
    private const int Nah210Kjv = 34_002_011;

    private static readonly TskLinkKey Ps336UnderGen11 = new(Gen11, Ps336, null);
    private static readonly TskLinkKey NahUnderGen12 = new(Gen12, Nah210Kjv, null);

    private static async Task WithTransactionAsync(Func<IDbConnection, IDbTransaction, Task> test)
    {
        var connectionString = Environment.GetEnvironmentVariable(EnvVar);
        Assert.SkipWhen(string.IsNullOrWhiteSpace(connectionString), $"{EnvVar} is not set.");

        using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        using var transaction = connection.BeginTransaction();

        // The tests assume plain TSK for 1Mo 1,1-2: curations a maintainer made in this database copy are removed inside the rolled-back transaction.
        await connection.ExecuteAsync("""
            DELETE FROM "unshackled-word"."TskCurations" c
            WHERE c."SourceHebRefId" IN (@Gen11, @Gen12)
            """, new { Gen11, Gen12 }, transaction);

        await test(connection, transaction);

        transaction.Rollback();
    }

    private static Task<int> NewUserAsync(IDbConnection c, IDbTransaction t)
    {
        var name = "tsk-" + Guid.NewGuid().ToString("N");

        return c.QuerySingleAsync<int>("""
            INSERT INTO "unshackled-word"."Users" ("Email", "EmailNormalized", "DisplayName")
            VALUES (@Name || '@example.org', @Name || '@example.org', @Name)
            RETURNING "Id"
            """, new { Name = name }, t);
    }

    private static Task<GetChapterCrossReferencesResponse> ChapterAsync(IDbConnection c, IDbTransaction t, bool includeHidden = false) =>
        new ChapterCrossReferenceService(new TskRepository(new TransactionDbReader(c, t))).GetAsync(1, 1, includeHidden, TestContext.Current.CancellationToken);

    private static List<CrossReferenceKey> Keys(GetChapterCrossReferencesResponse response, int verse) =>
        response.Verses.Single(v => v.Verse == verse).References.Select(r => new CrossReferenceKey(r.BibleBookId, r.Chapter, r.Verse, r.EndVerse)).ToList();

    private sealed record CrossReferenceKey(int Book, int Chapter, int Verse, int? EndVerse);

    private sealed record AuditRow(int SourceHebRefId, int TargetStartHebRefId, int? TargetEndHebRefId, string? Before, string? After, int? UserId, string Source);

    private static async Task<List<AuditRow>> AuditAsync(IDbConnection c, IDbTransaction t, int userId) =>
        (await c.QueryAsync<AuditRow>("""
            SELECT a."SourceHebRefId", a."TargetStartHebRefId", a."TargetEndHebRefId", a."Before"::text AS "Before", a."After"::text AS "After", a."UserId", a."Source"
            FROM "unshackled-word"."TskCurationAudit" a
            WHERE a."UserId" = @userId
            ORDER BY a."Id"
            """, new { userId }, t)).ToList();

    private static Task<int> CurationRowsAsync(IDbConnection c, IDbTransaction t, TskLinkKey key) =>
        c.ExecuteScalarAsync<int>("""
            SELECT count(*)
            FROM "unshackled-word"."TskCurations" c
            WHERE c."SourceHebRefId" = @SourceHebRefId
              AND c."TargetStartHebRefId" = @TargetStartHebRefId
              AND c."TargetEndHebRefId" IS NOT DISTINCT FROM @TargetEndHebRefId
            """, key, t);

    private static SaveTskCurationRequest Request(TskLinkKey key, string? rating, string? comment = null) => new()
    {
        SourceHebRefId = key.SourceHebRefId, TargetStartHebRefId = key.TargetStartHebRefId, TargetEndHebRefId = key.TargetEndHebRefId, Rating = rating, Comment = comment,
    };

    /// <summary>The 0024.3 "done when" scenario, step by step.</summary>
    [Fact]
    public async Task Scenario_HighlightPs336_HideNahUnderGen12_ThenNeutralDeletesTheRow() => await WithTransactionAsync(async (c, t) =>
    {
        var ct = TestContext.Current.CancellationToken;
        var maintainer = await NewUserAsync(c, t);
        var service = new TskCurationService(new TransactionRepository(c, t));

        var before = await ChapterAsync(c, t);
        Keys(before, 1)[0].ShouldNotBe(new CrossReferenceKey(19, 33, 6, null), "Ps 33,6 is not first in plain TSK");
        Keys(before, 2).ShouldContain(new CrossReferenceKey(34, 2, 11, null));

        var highlighted = await service.SaveAsync(maintainer, true, Request(Ps336UnderGen11, TskRatings.Highlight, "  Schöpfung durch das Wort "), ct);
        var hidden = await service.SaveAsync(maintainer, true, Request(NahUnderGen12, TskRatings.Hide), ct);

        highlighted.Status.ShouldBe(TskCurationStatus.Ok);
        highlighted.Response!.Rating.ShouldBe(TskRatings.Highlight);
        highlighted.Response.Comment.ShouldBe("Schöpfung durch das Wort");
        highlighted.Response.UpdatedAt.ShouldNotBeNull();
        hidden.Response!.Rating.ShouldBe(TskRatings.Hide);

        // anonymous reader: Ps 33,6 first with its comment, Nah 2,11 (KJV 2,10) gone, everything else in TSK order
        var after = await ChapterAsync(c, t);
        var verse1 = after.Verses.Single(v => v.Verse == 1).References;
        (verse1[0].BibleBookId, verse1[0].Chapter, verse1[0].Verse).ShouldBe((19, 33, 6));
        verse1[0].Rating.ShouldBe(TskRatings.Highlight);
        verse1[0].Comment.ShouldBe("Schöpfung durch das Wort");
        verse1.Skip(1).ShouldAllBe(r => r.Rating == null && r.Comment == null);
        Keys(after, 1).Skip(1).ShouldBe(Keys(before, 1).Where(k => k != new CrossReferenceKey(19, 33, 6, null)));
        Keys(after, 2).ShouldNotContain(new CrossReferenceKey(34, 2, 11, null));
        Keys(after, 2).ShouldBe(Keys(before, 2).Where(k => k != new CrossReferenceKey(34, 2, 11, null)));

        // curator view: the hidden link at its TSK place, marked
        var curator = await ChapterAsync(c, t, includeHidden: true);
        Keys(curator, 2).ShouldBe(Keys(before, 2));
        curator.Verses.Single(v => v.Verse == 2).References.Single(r => r.BibleBookId == 34).Rating.ShouldBe(TskRatings.Hide);

        var audit = await AuditAsync(c, t, maintainer);
        audit.Count.ShouldBe(2);
        audit.ShouldAllBe(a => a.Source == TskCurationSources.Edit && a.Before == null);
        audit[0].SourceHebRefId.ShouldBe(Gen11);
        audit[0].TargetStartHebRefId.ShouldBe(Ps336);
        var state = JsonSerializer.Deserialize<JsonElement>(audit[0].After!);
        state.GetProperty("rating").GetString().ShouldBe(TskRatings.Highlight);
        state.GetProperty("comment").GetString().ShouldBe("Schöpfung durch das Wort");
        state.GetProperty("origin").GetString().ShouldBe(TskOrigins.Tsk);

        // back to neutral without comment: the row goes, the audit records it
        var neutral = await service.SaveAsync(maintainer, true, Request(Ps336UnderGen11, null, "   "), ct);
        neutral.Response!.Rating.ShouldBeNull();
        neutral.Response.Comment.ShouldBeNull();
        neutral.Response.UpdatedAt.ShouldBeNull();
        (await CurationRowsAsync(c, t, Ps336UnderGen11)).ShouldBe(0);
        (await CurationRowsAsync(c, t, NahUnderGen12)).ShouldBe(1);
        audit = await AuditAsync(c, t, maintainer);
        audit.Count.ShouldBe(3);
        audit[2].Before.ShouldNotBeNull();
        audit[2].After.ShouldBeNull();
        Keys(await ChapterAsync(c, t), 1).ShouldBe(Keys(before, 1));
    });

    [Fact]
    public async Task NonMaintainer_IsForbidden_UnknownKey_IsNotFound_NothingWritten() => await WithTransactionAsync(async (c, t) =>
    {
        var ct = TestContext.Current.CancellationToken;
        var user = await NewUserAsync(c, t);
        var service = new TskCurationService(new TransactionRepository(c, t));

        (await service.SaveAsync(user, false, Request(Ps336UnderGen11, TskRatings.Highlight), ct)).Status.ShouldBe(TskCurationStatus.Forbidden);
        // Ps 33,7 is no TSK target of 1Mo 1,1; Spr 8,22-30 is, but not Spr 8,22-23
        (await service.SaveAsync(user, true, Request(new TskLinkKey(Gen11, 19_033_007, null), TskRatings.Highlight), ct)).Status.ShouldBe(TskCurationStatus.NotFound);
        (await service.SaveAsync(user, true, Request(new TskLinkKey(Gen11, 20_008_022, 20_008_023), TskRatings.Highlight), ct)).Status.ShouldBe(TskCurationStatus.NotFound);
        (await service.SaveAsync(user, true, Request(new TskLinkKey(Gen11, 20_008_022, null), TskRatings.Highlight), ct)).Status.ShouldBe(TskCurationStatus.NotFound);

        (await AuditAsync(c, t, user)).ShouldBeEmpty();
        (await CurationRowsAsync(c, t, Ps336UnderGen11)).ShouldBe(0);
    });

    [Fact]
    public async Task RangeKey_IsCurated_AndAnUnchangedSaveWritesNoAudit() => await WithTransactionAsync(async (c, t) =>
    {
        var ct = TestContext.Current.CancellationToken;
        var maintainer = await NewUserAsync(c, t);
        var service = new TskCurationService(new TransactionRepository(c, t));
        var prov = new TskLinkKey(Gen11, 20_008_022, 20_008_030);

        (await service.SaveAsync(maintainer, true, Request(prov, null, "Weisheit"), ct)).Response!.Comment.ShouldBe("Weisheit");
        (await service.SaveAsync(maintainer, true, Request(prov, null, "Weisheit "), ct)).Status.ShouldBe(TskCurationStatus.Ok);

        (await AuditAsync(c, t, maintainer)).Count.ShouldBe(1);
        var chapter = await ChapterAsync(c, t);
        var references = chapter.Verses.Single(v => v.Verse == 1).References;
        references.Single(r => r is { BibleBookId: 20, Verse: 22, EndVerse: 30 }).Comment.ShouldBe("Weisheit");
        references.Single(r => r is { BibleBookId: 20, Verse: 22, EndVerse: 24 }).Comment.ShouldBeNull();
    });

    [Fact]
    public async Task Constraints_RefuseInvalidRows() => await WithTransactionAsync(async (c, t) =>
    {
        foreach (var (name, values) in new[]
                 {
                     ("unknown rating", "'tsk', 'star', NULL, NULL, NULL, NULL, NULL"),
                     ("unknown origin", "'kjv', NULL, 'x', NULL, NULL, NULL, NULL"),
                     ("empty comment", "'tsk', NULL, '  ', NULL, NULL, NULL, NULL"),
                     ("word link without quote", "'tsk', NULL, NULL, 2, 2, NULL, NULL"),
                     ("word link backwards", "'tsk', NULL, NULL, 3, 2, 'x', NULL"),
                     ("range backwards", "'tsk', 'hide', NULL, NULL, NULL, NULL, 19033005"),
                     ("range across chapters", "'tsk', 'hide', NULL, NULL, NULL, NULL, 19034001"),
                 })
        {
            await c.ExecuteAsync("SAVEPOINT s", transaction: t);
            var ex = await Should.ThrowAsync<PostgresException>(() => c.ExecuteAsync($"""
                INSERT INTO "unshackled-word"."TskCurations" ("SourceHebRefId", "TargetStartHebRefId", "Origin", "Rating", "Comment", "StartPosition", "EndPosition", "QuotedText", "TargetEndHebRefId")
                VALUES (1001001, 19033006, {values})
                """, transaction: t), name);
            ex.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation, name);
            await c.ExecuteAsync("ROLLBACK TO SAVEPOINT s", transaction: t);
        }

        await c.ExecuteAsync("""
            INSERT INTO "unshackled-word"."TskCurations" ("SourceHebRefId", "TargetStartHebRefId", "Rating") VALUES (1001001, 19033006, 'highlight')
            """, transaction: t);
        var duplicate = await Should.ThrowAsync<PostgresException>(() => c.ExecuteAsync("""
            INSERT INTO "unshackled-word"."TskCurations" ("SourceHebRefId", "TargetStartHebRefId", "Rating") VALUES (1001001, 19033006, 'hide')
            """, transaction: t));
        duplicate.SqlState.ShouldBe(PostgresErrorCodes.UniqueViolation);
    });

    /// <summary>The repository's statics on the test's transaction.</summary>
    private sealed class TransactionRepository(IDbConnection connection, IDbTransaction transaction) : ITskCurationRepository
    {
        public Task<bool> TskLinkExistsAsync(TskLinkKey key, CancellationToken token = default) =>
            TskCurationRepository.TskLinkExistsAsync(connection, transaction, key, token);

        public Task<TskCurationRecord?> GetAsync(TskLinkKey key, CancellationToken token = default) =>
            TskCurationRepository.GetAsync(connection, transaction, key, token);

        public Task<TskCurationRecord?> ApplyAsync(TskLinkKey key, Func<TskCurationState, TskCurationState> change, int? userId, string source,
            CancellationToken token = default) =>
            TskCurationRepository.ApplyAsync(connection, transaction, key, change, userId, source, token);
    }

    private sealed class TransactionDbReader(IDbConnection connection, IDbTransaction transaction) : IDbReader
    {
        public Task<T?> ReadFirstOrDefaultAsync<T>(string sql, object? param = null) => connection.QueryFirstOrDefaultAsync<T>(sql, param, transaction);

        public async Task<IEnumerable<T>> ReadAsListAsync<T>(string sql, object? param = null) => await connection.QueryAsync<T>(sql, param, transaction);

        public Task<T?> ExecuteScalarAsync<T>(string sql, object? param = null) => connection.ExecuteScalarAsync<T>(sql, param, transaction);

        public Task<List<T>> ReadMultipleAsListAsync<T>(string sql, object? param, Func<IMultiDbReader, Task<List<T>>> mappingFunc, CancellationToken token = default) =>
            throw new NotSupportedException();
    }
}
