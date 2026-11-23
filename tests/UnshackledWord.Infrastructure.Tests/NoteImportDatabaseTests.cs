using System.Data;
using Dapper;
using Npgsql;
using UnshackledWord.Application.Features.Notes;
using UnshackledWord.Domain.WebApi.Notes;
using UnshackledWord.Infrastructure.Notes;

namespace UnshackledWord.Infrastructure.Tests;

/// <summary>
/// Roadmap 0023.6: export, delete the notes, import restores identical rows; a second import changes nothing; an unknown author is skipped.
/// Same setup as <see cref="NoteDatabaseTests"/>: one rolled-back transaction, own users, <c>UNSHACKLEDWORD_TEST_POSTGRES</c> or skipped.
/// </summary>
public class NoteImportDatabaseTests
{
    private const string EnvVar = "UNSHACKLEDWORD_TEST_POSTGRES";
    private const int Rom828 = 45008028;

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

    private static async Task<int> NewUserAsync(IDbConnection c, IDbTransaction t, string email)
    {
        return await c.QuerySingleAsync<int>("""
            INSERT INTO "unshackled-word"."Users" ("Email", "EmailNormalized", "DisplayName")
            VALUES (@Email, lower(@Email), 'Importer')
            RETURNING "Id"
            """, new { Email = email }, t);
    }

    private static Task<Guid> InsertAsync(IDbConnection c, IDbTransaction t, int userId, string kind, int? start, int? end, int? startPosition, int? endPosition, string? quoted,
        string visibility, string publicState, string body, string? approvedBody, int? approverId, bool hidden, string? reviewNote, bool edited) =>
        c.QuerySingleAsync<Guid>("""
            INSERT INTO "unshackled-word"."Notes" ("UserId", "Kind", "BibleBookId", "Chapter", "StartHebRefId", "EndHebRefId", "StartPosition", "EndPosition", "QuotedText",
                                                   "BodyMarkdown", "Visibility", "PublicState", "ApprovedBodyMarkdown", "ApprovedAt", "ApprovedByUserId", "ReviewNote", "HiddenAt", "UpdatedAt")
            VALUES (@UserId, @Kind, 45, 8, @Start, @End, @StartPosition, @EndPosition, @Quoted,
                    @Body, @Visibility, @PublicState, @ApprovedBody, CASE WHEN @ApprovedBody IS NULL THEN NULL ELSE now() END, @ApproverId, @ReviewNote,
                    CASE WHEN @Hidden THEN now() END, CASE WHEN @Edited THEN now() END)
            RETURNING "Id"
            """, new
        {
            UserId = userId, Kind = kind, Start = start, End = end, StartPosition = startPosition, EndPosition = endPosition, Quoted = quoted, Body = body,
            Visibility = visibility, PublicState = publicState, ApprovedBody = approvedBody, ApproverId = approverId, ReviewNote = reviewNote, Hidden = hidden, Edited = edited,
        }, t);

    private const string SelectRows = """
        SELECT "Id", "UserId", "Kind", "BibleBookId", "Chapter", "StartHebRefId", "EndHebRefId", "StartPosition", "EndPosition", "QuotedText", "BodyMarkdown", "Visibility",
               "PublicState", "ApprovedBodyMarkdown", "ApprovedAt", "ApprovedByUserId", "ReviewNote", "HiddenAt", "CreatedAt", "UpdatedAt"
        FROM "unshackled-word"."Notes" WHERE "UserId" = ANY(@Users) ORDER BY "Id"
        """;

    private static async Task<List<string>> SnapshotAsync(IDbConnection c, IDbTransaction t, int[] users) =>
        (await c.QueryAsync(SelectRows, new { Users = users }, t)).Select(r => string.Join("|", ((IDictionary<string, object?>)r).Values.Select(v => v?.ToString() ?? "null"))).ToList();

    [Fact]
    public async Task Import_ShouldRestoreIdenticalRows_ThenReportEverythingUnchanged_AndSkipUnknownAuthors()
    {
        await WithTransactionAsync(async (c, t) =>
        {
            var tag = Guid.NewGuid().ToString("N");
            var author = await NewUserAsync(c, t, $"author-{tag}@example.org");
            var maintainer = await NewUserAsync(c, t, $"maint-{tag}@example.org");
            var users = new[] { author, maintainer };

            await InsertAsync(c, t, author, NoteKinds.Chapter, null, null, null, null, null, "private", "none", "Kapitel", null, null, false, null, false);
            await InsertAsync(c, t, author, NoteKinds.Words, Rom828, Rom828, 4, 8, "denen, die Gott lieben", "internal", "none", "**Wort** – Umlaute äöü", null, null, false, null, true);
            await InsertAsync(c, t, author, NoteKinds.Verses, Rom828, Rom828 + 2, null, null, null, "public", "approved", "neu", "alt", maintainer, false, null, true);
            await InsertAsync(c, t, author, NoteKinds.Verse, Rom828, Rom828, null, null, null, "public", "rejected", "abgelehnt", null, null, false, "zu kurz", true);
            await InsertAsync(c, t, author, NoteKinds.Verse, Rom828, Rom828, null, null, null, "internal", "none", "versteckt", null, null, true, "unpassend", true);

            var repository = Substitute.For<INoteRepository>();
            repository.ListForExportAsync(Arg.Any<int?>(), Arg.Any<CancellationToken>())
                .Returns(_ => NoteRepository.ListForExportAsync(c, t, null).Result.Where(r => r.AuthorEmail.EndsWith(tag + "@example.org")).ToList());

            var before = await SnapshotAsync(c, t, users);
            var file = await new NoteExportService(repository).BuildAsync(null, "0.0.0", DateTime.UtcNow);
            file = NoteExportService.Deserialize(NoteExportService.Serialize(file))!;
            file.Notes.Count.ShouldBe(5);

            await c.ExecuteAsync("""DELETE FROM "unshackled-word"."Notes" WHERE "UserId" = ANY(@Users)""", new { Users = users }, t);

            var first = await NoteImporter.ImportAsync(c, t, file);
            first.Counts["inserted"].ShouldBe(5);
            first.Counts["updated"].ShouldBe(0);
            first.Counts["skipped"].ShouldBe(0);
            (await SnapshotAsync(c, t, users)).ShouldBe(before);

            var second = await NoteImporter.ImportAsync(c, t, file);
            second.Counts["inserted"].ShouldBe(0);
            second.Counts["updated"].ShouldBe(0);
            second.Counts["unchanged"].ShouldBe(5);

            var ghost = file.Notes[0];
            file.Notes.Add(new NoteExportItem
            {
                Id = Guid.NewGuid(), AuthorEmail = $"ghost-{tag}@example.org", Kind = ghost.Kind, BibleBookId = ghost.BibleBookId, Chapter = ghost.Chapter,
                BodyMarkdown = "x", Visibility = "private", PublicState = "none", CreatedAt = DateTime.UtcNow,
            });
            var third = await NoteImporter.ImportAsync(c, t, file);
            third.Counts["skipped"].ShouldBe(1);
            third.Counts["unchanged"].ShouldBe(5);
            third.Lists["skipped"].Items[0].ShouldContain("unknown author");
        });
    }

    [Fact]
    public async Task Import_ShouldUpdateOnlyNotesWithANewerChangeTime_AndKeepNotesMissingFromTheFile()
    {
        await WithTransactionAsync(async (c, t) =>
        {
            var tag = Guid.NewGuid().ToString("N");
            var author = await NewUserAsync(c, t, $"author-{tag}@example.org");
            var kept = await InsertAsync(c, t, author, NoteKinds.Chapter, null, null, null, null, null, "private", "none", "bleibt", null, null, false, null, false);
            var changed = await InsertAsync(c, t, author, NoteKinds.Chapter, null, null, null, null, null, "private", "none", "alt", null, null, false, null, true);

            var row = (await NoteRepository.ListForExportAsync(c, t, author)).Single(r => r.Id == changed);
            var item = new NoteExportItem
            {
                Id = row.Id, AuthorEmail = row.AuthorEmail, Kind = row.Kind, BibleBookId = row.BibleBookId, Chapter = row.Chapter, BodyMarkdown = "neu",
                Visibility = row.Visibility, PublicState = row.PublicState, CreatedAt = row.CreatedAt, UpdatedAt = row.UpdatedAt!.Value.AddMinutes(1),
            };

            var report = await NoteImporter.ImportAsync(c, t, new NoteExportFile { Notes = [item] });
            report.Counts["updated"].ShouldBe(1);
            (await c.QuerySingleAsync<string>("""SELECT "BodyMarkdown" FROM "unshackled-word"."Notes" WHERE "Id" = @Id""", new { Id = changed }, t)).ShouldBe("neu");
            (await c.ExecuteScalarAsync<bool>("""SELECT EXISTS (SELECT 1 FROM "unshackled-word"."Notes" WHERE "Id" = @Id)""", new { Id = kept }, t)).ShouldBeTrue();

            item.BodyMarkdown = "älter";
            item.UpdatedAt = row.UpdatedAt;
            (await NoteImporter.ImportAsync(c, t, new NoteExportFile { Notes = [item] })).Counts["unchanged"].ShouldBe(1);
        });
    }
}
