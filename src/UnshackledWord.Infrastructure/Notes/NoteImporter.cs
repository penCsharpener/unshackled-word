using System.Data;
using Dapper;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Application.Features.Notes;
using UnshackledWord.Domain.WebApi.Maintainer;

namespace UnshackledWord.Infrastructure.Notes;

/// <summary>
/// Imports a notes export file (roadmap 0023.6, <c>06-export-import.md</c>): upsert by <c>Id</c>, an existing note only when the file's change time is newer,
/// author and approver by <c>EmailNormalized</c>. Deletes nothing. The static method runs on a given connection and transaction so the database tests can roll back.
/// </summary>
public static class NoteImporter
{
    public static async Task<SeedStepReport> ImportAsync(IDbConnection c, IDbTransaction? t, NoteExportFile file, IProgress<string>? progress = null, CancellationToken token = default)
    {
        var emails = file.Notes
            .SelectMany(n => new[] { n.AuthorEmail, n.ApprovedByEmail })
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Select(e => EmailNormalizer.Normalize(e!))
            .Distinct()
            .ToArray();

        var users = (await c.QueryAsync<(string Email, int Id)>(new CommandDefinition(NoteImportSql.UsersByEmail, new { Emails = emails }, t, cancellationToken: token)))
            .ToDictionary(u => u.Email, u => u.Id);

        var existing = (await c.QueryAsync<(Guid Id, DateTime ChangedAt)>(new CommandDefinition(NoteImportSql.ExistingTimes, new { Ids = file.Notes.Select(n => n.Id).ToArray() }, t, cancellationToken: token)))
            .ToDictionary(e => e.Id, e => e.ChangedAt);

        int inserted = 0, updated = 0, unchanged = 0;
        var skipped = new List<string>();

        foreach (var note in file.Notes)
        {
            token.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(note.AuthorEmail) || !users.TryGetValue(EmailNormalizer.Normalize(note.AuthorEmail), out var authorId))
            {
                skipped.Add($"{note.Id}: unknown author {note.AuthorEmail}");

                continue;
            }

            int? approverId = !string.IsNullOrWhiteSpace(note.ApprovedByEmail) && users.TryGetValue(EmailNormalizer.Normalize(note.ApprovedByEmail), out var a) ? a : null;
            var fileChangedAt = note.UpdatedAt ?? note.CreatedAt;
            var isNew = !existing.TryGetValue(note.Id, out var currentChangedAt);

            if (!isNew && fileChangedAt <= currentChangedAt)
            {
                unchanged++;

                continue;
            }

            try
            {
                await c.ExecuteAsync(new CommandDefinition(isNew ? NoteImportSql.Insert : NoteImportSql.Update, new
                {
                    note.Id,
                    UserId = authorId,
                    note.Kind,
                    BookId = note.BibleBookId,
                    note.Chapter,
                    note.StartHebRefId,
                    note.EndHebRefId,
                    note.StartPosition,
                    note.EndPosition,
                    note.QuotedText,
                    note.BodyMarkdown,
                    note.Visibility,
                    note.PublicState,
                    note.ApprovedBodyMarkdown,
                    note.ApprovedAt,
                    ApprovedByUserId = approverId,
                    note.ReviewNote,
                    note.HiddenAt,
                    note.CreatedAt,
                    note.UpdatedAt,
                }, t, cancellationToken: token));
            }
            catch (Npgsql.PostgresException e)
            {
                skipped.Add($"{note.Id}: rejected by the database ({e.ConstraintName ?? e.SqlState})");

                continue;
            }

            if (isNew)
            {
                inserted++;
            }
            else
            {
                updated++;
            }
        }

        progress?.Report($"Notes: {inserted} inserted, {updated} updated, {unchanged} unchanged, {skipped.Count} skipped");

        return new SeedStepReport { Kind = "notes", Action = "import" }
            .WithCount("inserted", inserted).WithCount("updated", updated).WithCount("unchanged", unchanged).WithCount("skipped", skipped.Count)
            .AddList("skipped", skipped, s => s);
    }
}

internal static class NoteImportSql
{
    internal const string UsersByEmail = """
        SELECT "EmailNormalized" AS "Email", "Id"
        FROM "unshackled-word"."Users"
        WHERE "EmailNormalized" = ANY(@Emails)
        """;

    internal const string ExistingTimes = """
        SELECT "Id", COALESCE("UpdatedAt", "CreatedAt") AS "ChangedAt"
        FROM "unshackled-word"."Notes"
        WHERE "Id" = ANY(@Ids)
        """;

    internal const string Insert = """
        INSERT INTO "unshackled-word"."Notes" ("Id", "UserId", "Kind", "BibleBookId", "Chapter", "StartHebRefId", "EndHebRefId", "StartPosition", "EndPosition",
                                               "QuotedText", "BodyMarkdown", "Visibility", "PublicState", "ApprovedBodyMarkdown", "ApprovedAt", "ApprovedByUserId",
                                               "ReviewNote", "HiddenAt", "CreatedAt", "UpdatedAt")
        VALUES (@Id, @UserId, @Kind, @BookId, @Chapter, @StartHebRefId, @EndHebRefId, @StartPosition, @EndPosition,
                @QuotedText, @BodyMarkdown, @Visibility, @PublicState, @ApprovedBodyMarkdown, @ApprovedAt, @ApprovedByUserId,
                @ReviewNote, @HiddenAt, @CreatedAt, @UpdatedAt)
        """;

    internal const string Update = """
        UPDATE "unshackled-word"."Notes"
        SET "UserId"               = @UserId,
            "Kind"                 = @Kind,
            "BibleBookId"          = @BookId,
            "Chapter"              = @Chapter,
            "StartHebRefId"        = @StartHebRefId,
            "EndHebRefId"          = @EndHebRefId,
            "StartPosition"        = @StartPosition,
            "EndPosition"          = @EndPosition,
            "QuotedText"           = @QuotedText,
            "BodyMarkdown"         = @BodyMarkdown,
            "Visibility"           = @Visibility,
            "PublicState"          = @PublicState,
            "ApprovedBodyMarkdown" = @ApprovedBodyMarkdown,
            "ApprovedAt"           = @ApprovedAt,
            "ApprovedByUserId"     = @ApprovedByUserId,
            "ReviewNote"           = @ReviewNote,
            "HiddenAt"             = @HiddenAt,
            "CreatedAt"            = @CreatedAt,
            "UpdatedAt"            = @UpdatedAt
        WHERE "Id" = @Id
        """;
}
