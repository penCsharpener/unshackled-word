using System.Data;
using System.Text.Json.Nodes;
using Dapper;
using Npgsql;
using Microsoft.Extensions.Logging.Abstractions;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Features.Revision;
using UnshackledWord.Infrastructure.Repositories;
using UnshackledWord.Infrastructure.Seed.Revision;
using UnshackledWord.Infrastructure.Services;

namespace UnshackledWord.Infrastructure.Tests;

/// <summary>
/// Roadmap 0014: after every write path the newest <c>RevisionVerseAudit</c> row equals the live row, and the live table holds one row per verse and
/// revision (text save = update in place, withdraw = delete). Needs a real Postgres with migration 0024 applied; point <c>UNSHACKLEDWORD_TEST_POSTGRES</c> at a COPY of the dev database (never the dev database itself), otherwise the tests are skipped.
/// The tests write one verse (<see cref="HebRefId"/>) at level 200 and remove it afterwards.
/// </summary>
// The revision seed import takes LOCK TABLE on RevisionVerses; VocabRenderingsDatabaseTests refreshes StatsVocabRenderings (reads RevisionVerses)
// inside a long transaction. Same collection, so the two never wait on each other past the command timeout.
[Collection("VocabDatabase")]
public class RevisionAuditDatabaseTests
{
    private const string EnvVar = "UNSHACKLEDWORD_TEST_POSTGRES";
    private const int HebRefId = 66099099;
    private const int RealVerseHebRefId = 66022021; // Revelation 22:21: the service tests need a verse that exists in Elb1871Verses
    private const int Level = 200;
    private const string SeedVersion = "uw-0014-test";

    private static string? ConnectionString => Environment.GetEnvironmentVariable(EnvVar);

    private static IDbConnectionFactory Factory() => new TestFactory(ConnectionString!);

    private sealed class TestFactory : IDbConnectionFactory
    {
        private readonly string _connectionString;

        public TestFactory(string connectionString)
        {
            _connectionString = connectionString;
            DbConnection = new NpgsqlConnection(connectionString);
        }

        public IDbConnection DbConnection { get; set; }

        public Task BulkInsertAsync<T>(string tableName, string[] columns, ICollection<T> dataList, Action<IBinaryImporter, T> mapping, CancellationToken token = default) =>
            throw new NotSupportedException();

        public IDbConnection CreateDbConnection() => new NpgsqlConnection(_connectionString);

        public async Task<IDbConnection> CreateDbConnectionAsync(CancellationToken token = default)
        {
            var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(token);
            return connection;
        }

        public void Dispose() => DbConnection.Dispose();

        public ValueTask DisposeAsync()
        {
            DbConnection.Dispose();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class Db : IAsyncDisposable
    {
        public IDbConnectionFactory Factory { get; } = RevisionAuditDatabaseTests.Factory();
        public RevisionRepository Repository { get; }
        public int UserId { get; private set; }

        /// <summary>The verse the helpers below look at; the service test switches it to a verse that exists in Elb1871Verses.</summary>
        public int Ref { get; set; } = HebRefId;

        public Db()
        {
            Repository = new RevisionRepository(new DbReader(Factory, NullLogger<DbReader>.Instance), Factory);
        }

        public async Task InitAsync()
        {
            await CleanAsync();
            using var c = await Factory.CreateDbConnectionAsync();
            UserId = await c.ExecuteScalarAsync<int>("""SELECT min("Id") FROM "unshackled-word"."Users" """);
        }

        public async Task CleanAsync()
        {
            using var c = await Factory.CreateDbConnectionAsync();
            await c.ExecuteAsync(
                """
                DELETE FROM "unshackled-word"."RevisionVerses" WHERE "HebRefId" = ANY (@HebRefIds); -- words and mapping go by cascade
                DELETE FROM "unshackled-word"."RevisionVerseAudit" WHERE "HebRefId" = ANY (@HebRefIds);
                DELETE FROM "unshackled-word"."SeedVerseState" WHERE "HebRefId" = ANY (@HebRefIds);
                DELETE FROM "unshackled-word"."SeedImports" WHERE "SeedVersion" = @SeedVersion;
                """,
                new { HebRefIds = new[] { HebRefId, RealVerseHebRefId }, SeedVersion });
        }

        public async Task SaveMappingAsync(int revisionVerseId, params RevisionMappingRow[] rows) =>
            (await Repository.ReplaceMappingAsync(revisionVerseId, "G", rows, UserId)).ShouldBeTrue();

        public async Task<int> LiveRowCountAsync()
        {
            using var c = await Factory.CreateDbConnectionAsync();
            return await c.ExecuteScalarAsync<int>("""SELECT count(*) FROM "unshackled-word"."RevisionVerses" WHERE "HebRefId" = @HebRefId""", new { HebRefId = Ref });
        }

        public async Task<int> WordCountAsync()
        {
            using var c = await Factory.CreateDbConnectionAsync();
            return await c.ExecuteScalarAsync<int>(
                """
                SELECT count(*)
                FROM "unshackled-word"."RevisionWords" rw
                    JOIN "unshackled-word"."RevisionVerses" rv ON rv."Id" = rw."RevisionVerseId"
                WHERE rv."HebRefId" = @HebRefId
                """,
                new { HebRefId = Ref });
        }

        public async Task<List<AuditRow>> AuditAsync()
        {
            using var c = await Factory.CreateDbConnectionAsync();
            return (await c.QueryAsync<AuditRow>(
                """
                SELECT a."Action"           AS "Action"
                     , a."Version"          AS "Version"
                     , a."VerseText"        AS "VerseText"
                     , a."Note"             AS "Note"
                     , a."BaseTextHash"     AS "BaseTextHash"
                     , a."Mapping"::text    AS "Mapping"
                     , a."ByUserId"         AS "ByUserId"
                     , a."RevisionVerseId"  AS "RevisionVerseId"
                     , a."BasedOnVersion"   AS "BasedOnVersion"
                FROM "unshackled-word"."RevisionVerseAudit" a
                WHERE a."HebRefId" = @HebRefId
                ORDER BY a."Id"
                """,
                new { HebRefId = Ref })).ToList();
        }

        /// <summary>The newest audit row must be the live row: version, text, note, hash and the mapping snapshot rebuilt here from the live words and mapping.</summary>
        public async Task AssertNewestAuditEqualsLiveAsync(string expectedAction)
        {
            var audit = (await AuditAsync()).Last();
            audit.Action.ShouldBe(expectedAction);

            using var c = await Factory.CreateDbConnectionAsync();
            var live = await c.QueryFirstOrDefaultAsync<LiveRow>(
                """
                SELECT rv."Id" AS "Id", rv."Version" AS "Version", rv."VerseText" AS "VerseText", rv."Note" AS "Note", rv."BaseTextHash" AS "BaseTextHash"
                FROM "unshackled-word"."RevisionVerses" rv
                WHERE rv."HebRefId" = @HebRefId
                """,
                new { HebRefId = Ref });

            if (expectedAction == RevisionAuditAction.Withdraw)
            {
                live.ShouldBeNull();
                audit.Mapping.ShouldBeNull();
                return;
            }

            live.ShouldNotBeNull();
            audit.RevisionVerseId.ShouldBe(live.Id);
            audit.Version.ShouldBe(live.Version);
            audit.VerseText.ShouldBe(live.VerseText);
            audit.Note.ShouldBe(live.Note);
            audit.BaseTextHash.ShouldBe(live.BaseTextHash);

            var rows = (await c.QueryAsync<SnapshotRow>(
                """
                SELECT rw."PositionInVerse" AS "Pos", rm."StepWordId" AS "Step", rm."IsAddedWord" AS "Added", pw."PositionInVerse" AS "ParentPos", rm."GermanWordPart" AS "Part", left(rm."Origin", 1) AS "Origin"
                FROM "unshackled-word"."RevisionWords" rw
                    JOIN "unshackled-word"."RevisionMapping" rm ON rm."RevisionWordId" = rw."Id"
                    LEFT JOIN "unshackled-word"."RevisionWords" pw ON pw."Id" = rm."ParentRevisionWordId"
                WHERE rw."RevisionVerseId" = @Id
                ORDER BY rw."PositionInVerse", rm."Id"
                """,
                new { live.Id })).ToList();
            var expected = new JsonArray(rows.Select(r => (JsonNode)new JsonArray(r.Pos, r.Step, r.Added, r.ParentPos, r.Part, r.Origin)).ToArray());
            JsonNode.DeepEquals(JsonNode.Parse(audit.Mapping!), expected).ShouldBeTrue($"audit {audit.Mapping} vs live {expected}");
        }

        public async ValueTask DisposeAsync()
        {
            await CleanAsync();
            await Factory.DisposeAsync();
        }
    }

    private sealed class AuditRow
    {
        public string Action { get; set; } = "";
        public int Version { get; set; }
        public string VerseText { get; set; } = "";
        public string? Note { get; set; }
        public string BaseTextHash { get; set; } = "";
        public string? Mapping { get; set; }
        public int? ByUserId { get; set; }
        public int? RevisionVerseId { get; set; }
        public int? BasedOnVersion { get; set; }
    }

    private sealed class LiveRow
    {
        public int Id { get; set; }
        public int Version { get; set; }
        public string VerseText { get; set; } = "";
        public string? Note { get; set; }
        public string BaseTextHash { get; set; } = "";
    }

    private sealed class SnapshotRow
    {
        public int Pos { get; set; }
        public int? Step { get; set; }
        public bool Added { get; set; }
        public int? ParentPos { get; set; }
        public string? Part { get; set; }
        public string Origin { get; set; } = "";
    }

    private static RevisionMappingRow Manual(int wordId, int stepWordId) => new() { RevisionWordId = wordId, StepWordId = stepWordId, Origin = RevisionOrigin.Manual };

    private static NewRevisionVersion Save(int? expectedVersion, string text, int userId, int? basedOnRevisionId = null, int? basedOnVersion = null)
    {
        var tokens = RevisionTokenizer.Tokenize(text);
        return new NewRevisionVersion
        {
            HebRefId = HebRefId,
            Level = Level,
            ExpectedVersion = expectedVersion,
            VerseText = text,
            BaseTextHash = "abcdef123456",
            Note = expectedVersion is null ? "first" : null,
            CreatedByUserId = userId,
            BasedOnRevisionId = basedOnRevisionId,
            BasedOnVersion = basedOnVersion,
            Tokens = tokens,
            Words = tokens.Select(t => new RevisionCarryWord(t.Position, t.Position, RevisionCarry.Same)).ToList(),
            CarriedRows = [new RevisionCarryRow(1, 4711, false, null, null), new RevisionCarryRow(2, null, true, 1, "s")],
            Lang = "G",
        };
    }

    private static async Task<Db?> OpenAsync()
    {
        Assert.SkipWhen(string.IsNullOrWhiteSpace(ConnectionString), $"{EnvVar} is not set (needs a copy of the dev database with migration 0024).");
        var db = new Db();
        await db.InitAsync();
        return db;
    }

    [Fact]
    public async Task TextSave_MappingSave_AndWithdraw_EachEndWithAnAuditRowEqualToTheLiveState()
    {
        await using var db = (await OpenAsync())!;

        var saved1 = await db.Repository.SaveVersionAsync(Save(null, "Hallo Welt", db.UserId));
        saved1.ShouldNotBeNull();
        saved1.Version.ShouldBe(1);
        await db.AssertNewestAuditEqualsLiveAsync(RevisionAuditAction.Text);

        var words = await db.Repository.GetWordsAsync(saved1.RevisionVerseId);
        await db.SaveMappingAsync(saved1.RevisionVerseId, Manual(words[0].Id, 4712), Manual(words[1].Id, 4713));
        await db.AssertNewestAuditEqualsLiveAsync(RevisionAuditAction.Mapping);

        var saved2 = await db.Repository.SaveVersionAsync(Save(1, "Hallo liebe Welt", db.UserId));
        saved2.ShouldNotBeNull();
        saved2.Version.ShouldBe(2);
        await db.AssertNewestAuditEqualsLiveAsync(RevisionAuditAction.Text);

        (await db.Repository.EndVariantAsync(saved2.RevisionVerseId, db.UserId)).ShouldBeTrue();
        await db.AssertNewestAuditEqualsLiveAsync(RevisionAuditAction.Withdraw);

        (await db.Repository.EndVariantAsync(saved2.RevisionVerseId, db.UserId)).ShouldBeFalse(); // already withdrawn: no second audit row

        var audit = await db.AuditAsync();
        audit.Select(a => (a.Action, a.Version)).ShouldBe([("text", 1), ("mapping", 1), ("text", 2), ("withdraw", 2)]);
        audit.ShouldAllBe(a => a.ByUserId == db.UserId);

        var history = await db.Repository.GetHistoryAsync(HebRefId, Level);
        history.Select(h => h.Action).ShouldBe(["withdraw", "text", "mapping", "text"]);
    }

    [Fact]
    public async Task TextSave_UpdatesTheLiveRowInPlace_RewritesTheWords_AndResetsTheMappingReview()
    {
        await using var db = (await OpenAsync())!;

        var saved1 = (await db.Repository.SaveVersionAsync(Save(null, "Hallo Welt", db.UserId)))!;
        var words1 = await db.Repository.GetWordsAsync(saved1.RevisionVerseId);
        await db.SaveMappingAsync(saved1.RevisionVerseId, Manual(words1[0].Id, 4712));
        (await db.Repository.GetHeaderAsync(saved1.RevisionVerseId))!.MappingReviewedAt.ShouldNotBeNull();

        var saved2 = (await db.Repository.SaveVersionAsync(Save(1, "Hallo liebe Welt", db.UserId)))!;

        saved2.RevisionVerseId.ShouldBe(saved1.RevisionVerseId); // the id is stable across text saves
        saved2.Version.ShouldBe(2);
        (await db.LiveRowCountAsync()).ShouldBe(1);
        var header = (await db.Repository.GetHeaderAsync(saved2.RevisionVerseId))!;
        header.Version.ShouldBe(2);
        header.MappingReviewedAt.ShouldBeNull(); // a new text starts unreviewed
        var words2 = await db.Repository.GetWordsAsync(saved2.RevisionVerseId);
        words2.Select(w => w.WordInContext).ShouldBe(["Hallo", "liebe", "Welt"]);
        words2.Select(w => w.Id).ShouldNotContain(words1[0].Id); // words are rewritten with new ids
        (await db.WordCountAsync()).ShouldBe(3); // the old words are gone, not kept
        (await db.Repository.GetMappingAsync(saved2.RevisionVerseId)).Select(m => (m.StepWordId, m.Origin)).ShouldBe([(4711, "carried"), (null, "carried")]);
        await db.AssertNewestAuditEqualsLiveAsync(RevisionAuditAction.Text);
    }

    [Fact]
    public async Task TextSave_WithAStaleExpectedVersion_ChangesNothing_AndWritesNoAuditRow()
    {
        await using var db = (await OpenAsync())!;

        var saved1 = (await db.Repository.SaveVersionAsync(Save(null, "Hallo Welt", db.UserId)))!;
        (await db.Repository.SaveVersionAsync(Save(1, "Hallo liebe Welt", db.UserId)))!.Version.ShouldBe(2);

        (await db.Repository.SaveVersionAsync(Save(1, "Stale save", db.UserId))).ShouldBeNull(); // the editor still holds version 1
        (await db.Repository.SaveVersionAsync(Save(null, "Blind create", db.UserId))).ShouldBeNull(); // expects no live row but there is one

        var header = (await db.Repository.GetHeaderAsync(saved1.RevisionVerseId))!;
        header.Version.ShouldBe(2);
        (await db.AuditAsync()).Select(a => (a.Action, a.Version)).ShouldBe([("text", 1), ("text", 2)]);
        await db.AssertNewestAuditEqualsLiveAsync(RevisionAuditAction.Text);
    }

    [Fact]
    public async Task TextSave_AfterAWithdraw_ContinuesTheVersion_AndTheMappingSaveOfAStaleWordIdIsRejected()
    {
        await using var db = (await OpenAsync())!;

        var saved1 = (await db.Repository.SaveVersionAsync(Save(null, "Hallo Welt", db.UserId)))!;
        var staleWords = await db.Repository.GetWordsAsync(saved1.RevisionVerseId);
        var saved2 = (await db.Repository.SaveVersionAsync(Save(1, "Hallo liebe Welt", db.UserId)))!;
        (await db.Repository.EndVariantAsync(saved2.RevisionVerseId, db.UserId)).ShouldBeTrue();
        (await db.LiveRowCountAsync()).ShouldBe(0);
        (await db.WordCountAsync()).ShouldBe(0); // words and mapping are deleted with the row (cascade)

        var recreated = (await db.Repository.SaveVersionAsync(Save(null, "Neu", db.UserId)))!;

        recreated.Version.ShouldBe(3); // continues from the highest audited version
        (await db.Repository.SaveVersionAsync(Save(null, "Again", db.UserId))).ShouldBeNull();
        (await db.AuditAsync()).Select(a => (a.Action, a.Version)).ShouldBe([("text", 1), ("text", 2), ("withdraw", 2), ("text", 3)]);

        // The mapping editor loaded the words of version 1; their ids no longer exist.
        (await db.Repository.ReplaceMappingAsync(recreated.RevisionVerseId, "G", [new RevisionMappingRow { RevisionWordId = staleWords[0].Id, StepWordId = 1, Origin = RevisionOrigin.Manual }], db.UserId)).ShouldBeFalse();
        await db.AssertNewestAuditEqualsLiveAsync(RevisionAuditAction.Text);
    }

    [Fact]
    public async Task TextSave_AtTheSameLevelAsItsParent_CarriesTheMappingItIsAboutToDelete()
    {
        await using var db = (await OpenAsync())!;
        var verses = Substitute.For<UnshackledWord.Application.Repositories.IVerseMappingRepository>();
        verses.GetGermanWordsAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns([]);
        verses.GetOriginalWordsAsync(Arg.Any<bool>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns([]);
        verses.GetMappingRowsAsync(Arg.Any<bool>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns([]);
        db.Ref = RealVerseHebRefId;
        var service = new RevisionService(db.Repository, new RevisionCarryService(verses, db.Repository));

        var first = await service.SaveTextAsync(66, 22, 21, new UnshackledWord.Domain.WebApi.Revision.SaveRevisionVerseRequest { Level = Level, Text = "Die Gnade sei mit allen" }, db.UserId);
        first.Outcome.ShouldBe(SaveRevisionOutcome.Saved);
        var id = first.Saved!.RevisionVerseId;
        var words = await db.Repository.GetWordsAsync(id);
        await db.SaveMappingAsync(id, Manual(words[1].Id, 5001), Manual(words[3].Id, 5002));

        // Re-save at the same level, started from that same level: the source row is the very row the save rewrites.
        var second = await service.SaveTextAsync(66, 22, 21, new UnshackledWord.Domain.WebApi.Revision.SaveRevisionVerseRequest { Level = Level, Text = "Die Gnade sei mit euch allen", ExpectedVersion = 1, BasedOnLevel = Level }, db.UserId);

        second.Outcome.ShouldBe(SaveRevisionOutcome.Saved);
        second.Saved!.RevisionVerseId.ShouldBe(id);
        second.Saved.Version.ShouldBe(2);
        var rewritten = await db.Repository.GetWordsAsync(id);
        var mapping = await db.Repository.GetMappingAsync(id);
        mapping.Select(m => (rewritten.Single(w => w.Id == m.RevisionWordId).WordInContext, m.StepWordId, m.Origin)).ShouldBe([("Gnade", 5001, "carried"), ("mit", 5002, "carried")]);
        await db.AssertNewestAuditEqualsLiveAsync(RevisionAuditAction.Text);
        (await db.AuditAsync()).Last().BasedOnVersion.ShouldBe(1); // the parent (this very verse) was at version 1 when version 2 was written
    }

    [Fact]
    public async Task SeedImport_WritesTextAuditRowsWithoutUser_AndWithdrawsWhenTheVariantLeavesTheSeed()
    {
        await using var db = (await OpenAsync())!;
        var seed = new RevisionSeedRepository(new DbReader(db.Factory, NullLogger<DbReader>.Instance), db.Factory);

        RevisionVariantWrite Variant(string text, bool reviewed = false, int? parentLevel = null) => new()
        {
            HebRefId = HebRefId,
            Level = Level,
            VerseText = text,
            BaseHash = "abcdef123456",
            Lang = "G",
            Reviewed = reviewed,
            ParentLevel = parentLevel,
            Words = [new RevisionWordWrite(1, "Hallo", "Hallo", 1, "same"), new RevisionWordWrite(2, "Welt", "Welt", 2, "same")],
            Mapping = [new RevisionMappingWrite(1, 4711, false, null, null, RevisionOrigin.Seed), new RevisionMappingWrite(2, null, true, 1, "s", RevisionOrigin.Seed)],
        };

        using (var session = await seed.BeginWriteAsync(CancellationToken.None))
        {
            var id1 = await session.WriteVariantAsync(Variant("Hallo Welt", reviewed: true));
            var id2 = await session.WriteVariantAsync(Variant("Hallo Welt", reviewed: false, parentLevel: Level));
            id2.ShouldBe(id1); // the second write updates the live row in place
            await session.CompleteAsync(new RevisionImportRecord(SeedVersion, null, 2, 2, 0, 0, 0, null, null));
        }

        await db.AssertNewestAuditEqualsLiveAsync(RevisionAuditAction.Text);
        (await db.LiveRowCountAsync()).ShouldBe(1);
        (await db.AuditAsync()).Select(a => (a.Action, a.Version, a.ByUserId, a.BasedOnVersion)).ShouldBe([("text", 1, (int?)null, (int?)null), ("text", 2, (int?)null, 1)]);
        (await db.Repository.GetHeaderAsync(id1Of(await db.AuditAsync())))!.MappingReviewedAt.ShouldBeNull(); // the seed says "not reviewed" now

        using (var session = await seed.BeginWriteAsync(CancellationToken.None))
        {
            await session.EndActiveAsync(HebRefId, Level);
            await session.EndActiveAsync(HebRefId, Level); // nothing active any more: no second withdraw
            await session.CompleteAsync(new RevisionImportRecord(SeedVersion, null, 0, 0, 0, 0, 0, null, null));
        }

        await db.AssertNewestAuditEqualsLiveAsync(RevisionAuditAction.Withdraw);
        (await db.AuditAsync()).Select(a => a.Action).ShouldBe(["text", "text", "withdraw"]);
        (await db.LiveRowCountAsync()).ShouldBe(0);

        using (var session = await seed.BeginWriteAsync(CancellationToken.None))
        {
            await session.WriteVariantAsync(Variant("Hallo Welt"));
            await session.CompleteAsync(new RevisionImportRecord(SeedVersion, null, 1, 1, 0, 0, 0, null, null));
        }

        (await db.AuditAsync()).Select(a => (a.Action, a.Version)).ShouldBe([("text", 1), ("text", 2), ("withdraw", 2), ("text", 3)]); // continues after the withdraw
        await db.AssertNewestAuditEqualsLiveAsync(RevisionAuditAction.Text);
    }

    private static int id1Of(List<AuditRow> audit) => audit.Last().RevisionVerseId!.Value;
}
