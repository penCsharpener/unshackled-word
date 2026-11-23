using System.Data;
using System.Text.Json;
using Dapper;
using Npgsql;
using UnshackledWord.Application.Features.CrossReferences;
using UnshackledWord.Application.Features.TskCuration;
using UnshackledWord.Application.Features.VerseCounting;
using UnshackledWord.Application.Features.WordDetail;
using UnshackledWord.Application.Repositories;
using UnshackledWord.Domain.Models.Study;
using UnshackledWord.Domain.WebApi.Elberfelder.GetChapterCrossReferences;
using UnshackledWord.Domain.WebApi.Elberfelder.GetWordDetail;
using UnshackledWord.Domain.WebApi.Tsk;
using UnshackledWord.Infrastructure.Repositories;
using static UnshackledWord.Infrastructure.Tests.TskCurationDatabaseTests;

namespace UnshackledWord.Infrastructure.Tests;

/// <summary>
/// Roadmap 0024.5 against a real Postgres (migration 0045, seeded TSK and Elb1871): word links of TSK links via <see cref="TskCurationService"/>, their
/// read side in the chapter list (incl. <c>mappedWords</c> on a revision level) and the word box order of <see cref="WordDetailService"/>. Every test runs
/// in one rolled-back transaction with its own user. Skipped without <c>UNSHACKLEDWORD_TEST_POSTGRES</c>.
/// </summary>
public class TskCurationWordLinkDatabaseTests
{
    private const string EnvVar = "UNSHACKLEDWORD_TEST_POSTGRES";
    private const int Gen11 = 1_001_001;

    /// <summary>1Mo 1,1 in Elb1871: „Im(1) Anfang(2) schuf(3) Gott(4) die(5) Himmel(6) und(7) die(8) Erde.(9)“.</summary>
    private const int Anfang = 2;

    private const int Gott = 4;

    /// <summary>The stored TSK keys under 1Mo 1,1: Spr 8,22–24, Joh 1,1–3 and Heb 1,10.</summary>
    private static readonly TskLinkTargetKey Spr822 = new() { TargetStartHebRefId = 20_008_022, TargetEndHebRefId = 20_008_024 };

    private static readonly TskLinkTargetKey Joh11 = new() { TargetStartHebRefId = 43_001_001, TargetEndHebRefId = 43_001_003 };
    private static readonly TskLinkTargetKey Heb110 = new() { TargetStartHebRefId = 58_001_010 };

    private static readonly (int Book, int Chapter, int Verse, int? EndVerse)[] Linked = [(20, 8, 22, 24), (43, 1, 1, 3), (58, 1, 10, null)];

    private static async Task WithTransactionAsync(Func<IDbConnection, IDbTransaction, Task> test)
    {
        var connectionString = Environment.GetEnvironmentVariable(EnvVar);
        Assert.SkipWhen(string.IsNullOrWhiteSpace(connectionString), $"{EnvVar} is not set.");

        using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        using var transaction = connection.BeginTransaction();

        // Plain TSK for 1Mo 1,1: curations made in this database copy are removed inside the rolled-back transaction.
        await connection.ExecuteAsync("""
            DELETE FROM "unshackled-word"."TskCurations" c
            WHERE c."SourceHebRefId" = @Gen11
            """, new { Gen11 }, transaction);

        await test(connection, transaction);

        transaction.Rollback();
    }

    private static Task<int> NewUserAsync(IDbConnection c, IDbTransaction t)
    {
        var name = "tsk-word-" + Guid.NewGuid().ToString("N");

        return c.QuerySingleAsync<int>("""
            INSERT INTO "unshackled-word"."Users" ("Email", "EmailNormalized", "DisplayName")
            VALUES (@Name || '@example.org', @Name || '@example.org', @Name)
            RETURNING "Id"
            """, new { Name = name }, t);
    }

    private static TskCurationService Service(IDbConnection c, IDbTransaction t) => new(new TransactionRepository(c, t));

    private static SaveTskWordLinkRequest Link(int start, int end, params TskLinkTargetKey[] keys) =>
        new() { SourceHebRefId = Gen11, Keys = [.. keys], StartPosition = start, EndPosition = end };

    private static Task<GetChapterCrossReferencesResponse> ChapterAsync(IDbConnection c, IDbTransaction t, int level = 0) =>
        new ChapterCrossReferenceService(new TskRepository(new TransactionDbReader(c, t))).GetAsync(1, 1, level: level, token: TestContext.Current.CancellationToken);

    private static List<CrossReferenceResponse> Verse1(GetChapterCrossReferencesResponse response) => response.Verses.Single(v => v.Verse == 1).References;

    private static (int, int, int, int?) Key(CrossReferenceResponse r) => (r.BibleBookId, r.Chapter, r.Verse, r.EndVerse);

    /// <summary>The word box of one 1Mo 1,1 word: only the cross references matter, so readings, morphemes and lexicon are empty fakes.</summary>
    private static async Task<List<CrossReferenceResponse>> WordBoxAsync(IDbConnection c, IDbTransaction t, int position, string wordInContext)
    {
        var service = new WordDetailService(Substitute.For<IWordDetailRepository>(), Substitute.For<IHebrewMorphemeRepository>(),
            Substitute.For<IStrongsLexiconRepository>(), new TskRepository(new TransactionDbReader(c, t)), Substitute.For<IVerseCountingMapProvider>());
        var word = new ElbWordRow { Id = 1, BibleBookId = 1, Chapter = 1, Verse = 1, HebRefId = Gen11, PositionInVerse = position, WordInContext = wordInContext };
        var response = await service.BuildAsync(word, [], [], "ai", TestContext.Current.CancellationToken);

        return response.CrossReferences.ShouldNotBeNull();
    }

    private static Task<int> RowsAsync(IDbConnection c, IDbTransaction t) =>
        c.ExecuteScalarAsync<int>("""SELECT count(*) FROM "unshackled-word"."TskCurations" c WHERE c."SourceHebRefId" = @Gen11""", new { Gen11 }, t);

    private static async Task<List<(string? Before, string? After)>> AuditAsync(IDbConnection c, IDbTransaction t, int userId) =>
        (await c.QueryAsync<(string?, string?)>("""
            SELECT a."Before"::text, a."After"::text
            FROM "unshackled-word"."TskCurationAudit" a
            WHERE a."UserId" = @userId
            ORDER BY a."Id"
            """, new { userId }, t)).ToList();

    /// <summary>The 0024.5 "done when" scenario.</summary>
    [Fact]
    public async Task Scenario_ThreeRefsLinkedToAnfang_ListedFirstInItsWordBox_DriftedAfterTheWordChanges() => await WithTransactionAsync(async (c, t) =>
    {
        var ct = TestContext.Current.CancellationToken;
        var maintainer = await NewUserAsync(c, t);
        var words = (await TskCurationRepositoryWords(c, t)).ToDictionary(w => w.PositionInVerse, w => w.WordInContext);
        words[Anfang].ShouldBe("Anfang");
        words[Gott].ShouldBe("Gott");

        var plainBox = (await WordBoxAsync(c, t, Anfang, "Anfang")).Select(Key).ToList();
        plainBox.Take(3).ShouldNotBe(Linked);
        var plainVerse = Verse1(await ChapterAsync(c, t)).Select(Key).ToList();
        plainVerse.ShouldContain(Linked[0]);

        var outcome = await Service(c, t).SaveWordLinkAsync(maintainer, true, Link(Anfang, Anfang, Spr822, Joh11, Heb110), ct);

        outcome.Status.ShouldBe(TskCurationStatus.Ok);
        var links = outcome.Response!.Links;
        links.Select(l => (l.TargetStartHebRefId, l.TargetEndHebRefId)).ShouldBe([(20_008_022, 20_008_024), (43_001_001, 43_001_003), (58_001_010, (int?)null)]);
        links.ShouldAllBe(l => l.Origin == TskOrigins.Tsk && l.Rating == null && l.Word!.StartPosition == Anfang && l.Word.EndPosition == Anfang
                               && l.Word.QuotedText == "Anfang" && l.Word.State == TskWordLinkStates.Ok);
        (await RowsAsync(c, t)).ShouldBe(3);
        var audit = await AuditAsync(c, t, maintainer);
        audit.Count.ShouldBe(3);
        audit.ShouldAllBe(a => a.Before == null);
        var after = JsonSerializer.Deserialize<JsonElement>(audit[0].After!);
        after.GetProperty("startPosition").GetInt32().ShouldBe(Anfang);
        after.GetProperty("quotedText").GetString().ShouldBe("Anfang");

        // chapter list: the three carry the word, the others none; the TSK order is unchanged
        var verse1 = Verse1(await ChapterAsync(c, t));
        verse1.Where(r => r.Word is not null).Select(Key).ShouldBe(Linked, ignoreOrder: true);
        verse1.Where(r => r.Word is not null).ShouldAllBe(r => r.Word!.State == TskWordLinkStates.Ok && r.Word.StartPosition == Anfang && r.Word.MappedWords == null);

        // word box of „Anfang“: these three first (in TSK order among themselves), then the rest as before; „Gott“ is unchanged
        var box = (await WordBoxAsync(c, t, Anfang, "Anfang")).Select(Key).ToList();
        box.Take(3).ShouldBe(Linked, ignoreOrder: true);
        box.ShouldBe(plainVerse.Where(k => Linked.Contains(k)).Concat(plainVerse.Where(k => !Linked.Contains(k))).Take(box.Count));
        (await WordBoxAsync(c, t, Gott, "Gott")).Select(Key).ShouldBe(plainBox);

        // the word changes: drifted, no positions, quote kept, not first in the word box any more
        await c.ExecuteAsync("""
            UPDATE "unshackled-word"."Elb1871Words"
            SET "WordInContext" = 'Beginn'
            WHERE "HebRefId" = @Gen11 AND "PositionInVerse" = @Anfang
            """, new { Gen11, Anfang }, t);
        var drifted = Verse1(await ChapterAsync(c, t)).Where(r => r.Word is not null).ToList();
        drifted.Select(Key).ShouldBe(Linked, ignoreOrder: true);
        drifted.ShouldAllBe(r => r.Word!.State == TskWordLinkStates.Drifted && r.Word.StartPosition == null && r.Word.EndPosition == null && r.Word.QuotedText == "Anfang");
        (await WordBoxAsync(c, t, Anfang, "Beginn")).Select(Key).ShouldBe(plainBox);

        // removing the word links: plain TSK links again, rows gone, one more audit row per link
        var removed = await Service(c, t).DeleteWordLinkAsync(maintainer, true,
            new DeleteTskWordLinkRequest { SourceHebRefId = Gen11, Keys = [Spr822, Joh11, Heb110] }, ct);
        removed.Status.ShouldBe(TskCurationStatus.Ok);
        removed.Response!.Links.ShouldAllBe(l => l.Word == null && l.UpdatedAt == null);
        (await RowsAsync(c, t)).ShouldBe(0);
        (await AuditAsync(c, t, maintainer)).Count.ShouldBe(6);
        Verse1(await ChapterAsync(c, t)).ShouldAllBe(r => r.Word == null);
    });

    private static Task<IReadOnlyList<UnshackledWord.Application.Features.Notes.NoteWordRow>> TskCurationRepositoryWords(IDbConnection c, IDbTransaction t) =>
        UnshackledWord.Infrastructure.TskCuration.TskCurationRepository.GetElbWordsAsync(c, t, Gen11, TestContext.Current.CancellationToken);

    [Fact]
    public async Task WordLink_KeepsRatingCommentAndAddedOrigin_AndDeleteKeepsTheRows() => await WithTransactionAsync(async (c, t) =>
    {
        var ct = TestContext.Current.CancellationToken;
        var maintainer = await NewUserAsync(c, t);
        var service = Service(c, t);
        (await service.SaveAsync(maintainer, true, new SaveTskCurationRequest
        {
            SourceHebRefId = Gen11, TargetStartHebRefId = 58_001_010, Rating = TskRatings.Highlight, Comment = "Sohn als Schöpfer",
        }, ct)).Status.ShouldBe(TskCurationStatus.Ok);
        (await service.AddAsync(maintainer, true, new AddTskCurationRequest { SourceHebRefId = Gen11, Reference = "Röm 8,28-30", Comment = "Plan" }, ct))
            .Status.ShouldBe(TskCurationStatus.Ok);
        var added = new TskLinkTargetKey { TargetStartHebRefId = 45_008_028, TargetEndHebRefId = 45_008_030 };

        var outcome = await service.SaveWordLinkAsync(maintainer, true, Link(6, 9, Heb110, added), ct);

        outcome.Status.ShouldBe(TskCurationStatus.Ok);
        var (heb, rom) = (outcome.Response!.Links[0], outcome.Response.Links[1]);
        (heb.Origin, heb.Rating, heb.Comment, heb.Word!.QuotedText).ShouldBe((TskOrigins.Tsk, TskRatings.Highlight, "Sohn als Schöpfer", "Himmel und die Erde."));
        (rom.Origin, rom.Comment, rom.Word!.StartPosition, rom.Word.EndPosition).ShouldBe((TskOrigins.Added, "Plan", 6, 9));

        // the verse texts and chapter list carry the word; „Erde.“ vs. „Erde“ does not drift (punctuation is ignored)
        await c.ExecuteAsync("""
            UPDATE "unshackled-word"."Elb1871Words" SET "WordInContext" = 'Erde' WHERE "HebRefId" = @Gen11 AND "PositionInVerse" = 9
            """, new { Gen11 }, t);
        var verse1 = Verse1(await ChapterAsync(c, t));
        verse1.Single(r => Key(r) == (58, 1, 10, null)).Word!.State.ShouldBe(TskWordLinkStates.Ok);
        verse1.Single(r => Key(r) == (45, 8, 28, 30)).Word!.State.ShouldBe(TskWordLinkStates.Ok);

        // removing the word link keeps the highlighted TSK row and the added row
        var removed = await service.DeleteWordLinkAsync(maintainer, true, new DeleteTskWordLinkRequest { SourceHebRefId = Gen11, Keys = [Heb110, added] }, ct);
        removed.Response!.Links.ShouldAllBe(l => l.Word == null && l.UpdatedAt != null);
        (removed.Response.Links[0].Rating, removed.Response.Links[1].Origin).ShouldBe((TskRatings.Highlight, TskOrigins.Added));
        (await RowsAsync(c, t)).ShouldBe(2);

        // deleting again changes nothing and writes no audit row
        var auditCount = (await AuditAsync(c, t, maintainer)).Count;
        await service.DeleteWordLinkAsync(maintainer, true, new DeleteTskWordLinkRequest { SourceHebRefId = Gen11, Keys = [Heb110] }, ct);
        (await AuditAsync(c, t, maintainer)).Count.ShouldBe(auditCount);
    });

    [Fact]
    public async Task WordLink_IsRefusedWithoutWriting_ForAForeignKey_MissingPositions_OrANonMaintainer() => await WithTransactionAsync(async (c, t) =>
    {
        var ct = TestContext.Current.CancellationToken;
        var maintainer = await NewUserAsync(c, t);
        var service = Service(c, t);
        var foreign = new TskLinkTargetKey { TargetStartHebRefId = 45_008_028 };

        (await service.SaveWordLinkAsync(maintainer, true, Link(Anfang, Anfang, Spr822, foreign), ct)).Status.ShouldBe(TskCurationStatus.NotFound);
        (await service.DeleteWordLinkAsync(maintainer, true, new DeleteTskWordLinkRequest { SourceHebRefId = Gen11, Keys = [foreign] }, ct))
            .Status.ShouldBe(TskCurationStatus.NotFound);
        (await service.SaveWordLinkAsync(maintainer, true, Link(9, 10, Spr822), ct)).Status.ShouldBe(TskCurationStatus.BadRequest);
        (await service.SaveWordLinkAsync(maintainer, true, Link(42, 42, Spr822), ct)).Status.ShouldBe(TskCurationStatus.BadRequest);
        (await service.SaveWordLinkAsync(maintainer, false, Link(Anfang, Anfang, Spr822), ct)).Status.ShouldBe(TskCurationStatus.Forbidden);

        (await RowsAsync(c, t)).ShouldBe(0);
        (await AuditAsync(c, t, maintainer)).ShouldBeEmpty();
    });

    [Fact]
    public async Task ChapterAtARevisionLevel_MapsTheMarkerToTheVariantWordsByBasePosition() => await WithTransactionAsync(async (c, t) =>
    {
        var ct = TestContext.Current.CancellationToken;
        var maintainer = await NewUserAsync(c, t);
        await Service(c, t).SaveWordLinkAsync(maintainer, true, Link(Anfang, Anfang, Joh11), ct);
        await Service(c, t).SaveWordLinkAsync(maintainer, true, Link(1, 1, Heb110), ct);

        // a test revision above every existing level with a variant of 1Mo 1,1: „Am(1→base 1) Uranfang(2→base 2)“ moved behind a new word, base 1 dropped
        var level = await c.QuerySingleAsync<int>("""SELECT COALESCE(MAX("Level") FILTER (WHERE "Level" < 1000), 200) + 1 FROM "unshackled-word"."Revisions" """, transaction: t);
        var revisionId = await c.QuerySingleAsync<int>("""
            INSERT INTO "unshackled-word"."Revisions" ("Level", "Code", "NameDe", "NameEn")
            VALUES (@Level, 'tsk-word-test', 'TSK-Wort-Test', 'TSK word test')
            RETURNING "Id"
            """, new { Level = level }, t);
        var verseId = await c.QuerySingleAsync<int>("""
            INSERT INTO "unshackled-word"."RevisionVerses" ("HebRefId", "RevisionId", "Version", "VerseText", "BaseTextHash")
            VALUES (@HebRefId, @RevisionId, 1, 'x', 'x')
            RETURNING "Id"
            """, new { HebRefId = Gen11, RevisionId = revisionId }, t);
        var variantWords = new (int Position, int? Base, string Change)[] { (1, null, "new"), (2, null, "new"), (3, 2, "changed"), (4, 3, "same") };
        foreach (var (position, basePosition, change) in variantWords)
        {
            await c.ExecuteAsync("""
                INSERT INTO "unshackled-word"."RevisionWords" ("RevisionVerseId", "PositionInVerse", "WordInContext", "BasePositionInVerse", "Change")
                VALUES (@VerseId, @Position, 'w', @Base, @Change)
                """, new { VerseId = verseId, Position = position, Base = basePosition, Change = change }, t);
        }

        var verse1 = Verse1(await ChapterAsync(c, t, level));
        var joh = verse1.Single(r => Key(r) == (43, 1, 1, 3)).Word.ShouldNotBeNull();
        joh.StartPosition.ShouldBe(Anfang);
        joh.MappedWords.ShouldNotBeNull().Select(w => (w.PositionInVerse, w.BasePositionInVerse)).ShouldBe([(3, Anfang)]);

        // base position 1 has no variant word: empty list (marker at verse level)
        verse1.Single(r => Key(r) == (58, 1, 10, null)).Word.ShouldNotBeNull().MappedWords.ShouldNotBeNull().ShouldBeEmpty();

        // Elb1871 (level 0) and a level without that variant carry no mapping
        Verse1(await ChapterAsync(c, t)).Single(r => Key(r) == (43, 1, 1, 3)).Word!.MappedWords.ShouldBeNull();
        Verse1(await ChapterAsync(c, t, 200)).Single(r => Key(r) == (43, 1, 1, 3)).Word!.MappedWords.ShouldBeNull();
    });
}
