using System.Data;
using Dapper;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Features.Notes;

namespace UnshackledWord.Infrastructure.Notes;

/// <summary>
/// Dapper access to <c>Notes</c> (roadmap 0023). The instance methods open a connection; the static ones run on a given connection and
/// transaction so the database tests can roll back.
/// </summary>
public sealed class NoteRepository : INoteRepository
{
    private readonly IDbConnectionFactory _factory;

    public NoteRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<bool> VerseExistsAsync(int hebRefId, CancellationToken token = default)
    {
        using var connection = await _factory.CreateDbConnectionAsync(token);

        return await VerseExistsAsync(connection, null, hebRefId, token);
    }

    public async Task<bool> ChapterExistsAsync(int bookId, int chapter, CancellationToken token = default)
    {
        using var connection = await _factory.CreateDbConnectionAsync(token);

        return await ChapterExistsAsync(connection, null, bookId, chapter, token);
    }

    public async Task<Guid> InsertAsync(int userId, NoteAnchor anchor, string bodyMarkdown, CancellationToken token = default)
    {
        using var connection = await _factory.CreateDbConnectionAsync(token);

        return await InsertAsync(connection, null, userId, anchor, bodyMarkdown, token);
    }

    public async Task<bool> UpdateAsync(Guid id, int userId, NoteAnchor? anchor, string bodyMarkdown, CancellationToken token = default)
    {
        using var connection = await _factory.CreateDbConnectionAsync(token);

        return await UpdateAsync(connection, null, id, userId, anchor, bodyMarkdown, token);
    }

    public async Task<bool> DeleteAsync(Guid id, int userId, CancellationToken token = default)
    {
        using var connection = await _factory.CreateDbConnectionAsync(token);

        return await DeleteAsync(connection, null, id, userId, token);
    }

    public async Task<NoteRecord?> GetAsync(Guid id, int? viewerId, CancellationToken token = default)
    {
        using var connection = await _factory.CreateDbConnectionAsync(token);

        return await GetAsync(connection, null, id, viewerId, token);
    }

    public async Task<IReadOnlyList<NoteRecord>> ListChapterAsync(int bookId, int chapter, int? viewerId, bool others, CancellationToken token = default)
    {
        using var connection = await _factory.CreateDbConnectionAsync(token);

        return await ListChapterAsync(connection, null, bookId, chapter, viewerId, others, token);
    }

    public static Task<bool> VerseExistsAsync(IDbConnection c, IDbTransaction? t, int hebRefId, CancellationToken token = default) =>
        c.ExecuteScalarAsync<bool>(new CommandDefinition(NoteSql.VerseExists, new { HebRefId = hebRefId }, t, cancellationToken: token));

    public static Task<bool> ChapterExistsAsync(IDbConnection c, IDbTransaction? t, int bookId, int chapter, CancellationToken token = default) =>
        c.ExecuteScalarAsync<bool>(new CommandDefinition(NoteSql.ChapterExists, new
        {
            From = bookId * 1_000_000 + chapter * 1_000,
            To = bookId * 1_000_000 + chapter * 1_000 + 999,
        }, t, cancellationToken: token));

    public static Task<Guid> InsertAsync(IDbConnection c, IDbTransaction? t, int userId, NoteAnchor anchor, string bodyMarkdown, CancellationToken token = default) =>
        c.ExecuteScalarAsync<Guid>(new CommandDefinition(NoteSql.Insert, new
        {
            UserId = userId,
            anchor.Kind,
            anchor.BookId,
            anchor.Chapter,
            anchor.StartHebRefId,
            anchor.EndHebRefId,
            anchor.StartPosition,
            anchor.EndPosition,
            anchor.QuotedText,
            BodyMarkdown = bodyMarkdown,
        }, t, cancellationToken: token));

    public static async Task<bool> UpdateAsync(IDbConnection c, IDbTransaction? t, Guid id, int userId, NoteAnchor? anchor, string bodyMarkdown, CancellationToken token = default)
    {
        var sql = anchor is null ? NoteSql.UpdateBody : NoteSql.UpdateBodyAndAnchor;
        var affected = await c.ExecuteAsync(new CommandDefinition(sql, new
        {
            Id = id,
            UserId = userId,
            Kind = anchor?.Kind,
            BookId = anchor?.BookId,
            Chapter = anchor?.Chapter,
            StartHebRefId = anchor?.StartHebRefId,
            EndHebRefId = anchor?.EndHebRefId,
            StartPosition = anchor?.StartPosition,
            EndPosition = anchor?.EndPosition,
            QuotedText = anchor?.QuotedText,
            BodyMarkdown = bodyMarkdown,
        }, t, cancellationToken: token));

        return affected == 1;
    }

    public static async Task<bool> DeleteAsync(IDbConnection c, IDbTransaction? t, Guid id, int userId, CancellationToken token = default) =>
        await c.ExecuteAsync(new CommandDefinition(NoteSql.Delete, new { Id = id, UserId = userId }, t, cancellationToken: token)) == 1;

    public static Task<NoteRecord?> GetAsync(IDbConnection c, IDbTransaction? t, Guid id, int? viewerId, CancellationToken token = default) =>
        c.QuerySingleOrDefaultAsync<NoteRecord>(new CommandDefinition(NoteSql.Get, new { Id = id, ViewerId = viewerId }, t, cancellationToken: token));

    public static async Task<IReadOnlyList<NoteRecord>> ListChapterAsync(IDbConnection c, IDbTransaction? t, int bookId, int chapter, int? viewerId, bool others, CancellationToken token = default) =>
        (await c.QueryAsync<NoteRecord>(new CommandDefinition(NoteSql.ListChapter, new
        {
            BookId = bookId,
            Chapter = chapter,
            ViewerId = viewerId,
            Others = others,
        }, t, cancellationToken: token))).ToList();
}

internal static class NoteSql
{
    /// <summary>
    /// Who sees what (03-model-and-anchoring.md): anonymous (<c>@ViewerId</c> null) only approved public notes that are not hidden;
    /// a logged-in viewer all own notes plus others' internal/public notes that are not hidden. Needs the alias <c>n</c>.
    /// </summary>
    internal const string VisibilityFilter = """
        (
            (@ViewerId IS NULL
             AND n."Visibility" = 'public'
             AND n."ApprovedBodyMarkdown" IS NOT NULL
             AND n."HiddenAt" IS NULL)
            OR (@ViewerId IS NOT NULL
                AND (n."UserId" = @ViewerId
                     OR (n."Visibility" IN ('internal', 'public')
                         AND n."HiddenAt" IS NULL)))
        )
        """;

    /// <summary>Anonymous readers get the approved snapshot, everyone else the current text.</summary>
    private const string Columns = """
        n."Id"                                                                        AS "Id",
               n."UserId"                                                                    AS "UserId",
               u."DisplayName"                                                               AS "AuthorDisplayName",
               n."Kind"                                                                      AS "Kind",
               n."BibleBookId"                                                               AS "BibleBookId",
               n."Chapter"                                                                   AS "Chapter",
               n."StartHebRefId"                                                             AS "StartHebRefId",
               n."EndHebRefId"                                                               AS "EndHebRefId",
               n."StartPosition"                                                             AS "StartPosition",
               n."EndPosition"                                                               AS "EndPosition",
               n."QuotedText"                                                                AS "QuotedText",
               CASE WHEN @ViewerId IS NULL THEN n."ApprovedBodyMarkdown" ELSE n."BodyMarkdown" END AS "BodyMarkdown",
               n."Visibility"                                                                AS "Visibility",
               n."PublicState"                                                               AS "PublicState",
               n."HiddenAt" IS NOT NULL                                                      AS "IsHidden",
               n."CreatedAt"                                                                 AS "CreatedAt",
               n."UpdatedAt"                                                                 AS "UpdatedAt"
        """;

    internal const string VerseExists = """
        SELECT EXISTS (SELECT 1
                       FROM "unshackled-word"."Elb1871Verses" v
                       WHERE v."HebRefId" = @HebRefId)
        """;

    internal const string ChapterExists = """
        SELECT EXISTS (SELECT 1
                       FROM "unshackled-word"."Elb1871Verses" v
                       WHERE v."HebRefId" BETWEEN @From AND @To)
        """;

    internal const string Insert = """
        INSERT INTO "unshackled-word"."Notes" ("UserId", "Kind", "BibleBookId", "Chapter", "StartHebRefId", "EndHebRefId",
                                               "StartPosition", "EndPosition", "QuotedText", "BodyMarkdown")
        VALUES (@UserId, @Kind, @BookId, @Chapter, @StartHebRefId, @EndHebRefId,
                @StartPosition, @EndPosition, @QuotedText, @BodyMarkdown)
        RETURNING "Id"
        """;

    /// <summary>An edit of an approved public note sends it back to the review queue; anonymous readers keep the approved snapshot.</summary>
    private const string PublicStateOnEdit = """
        "PublicState" = CASE WHEN "PublicState" = 'approved' THEN 'pending' ELSE "PublicState" END
        """;

    internal const string UpdateBody = $"""
        UPDATE "unshackled-word"."Notes"
        SET "BodyMarkdown" = @BodyMarkdown,
            {PublicStateOnEdit},
            "UpdatedAt" = now()
        WHERE "Id" = @Id
          AND "UserId" = @UserId
        """;

    internal const string UpdateBodyAndAnchor = $"""
        UPDATE "unshackled-word"."Notes"
        SET "BodyMarkdown" = @BodyMarkdown,
            "Kind" = @Kind,
            "BibleBookId" = @BookId,
            "Chapter" = @Chapter,
            "StartHebRefId" = @StartHebRefId,
            "EndHebRefId" = @EndHebRefId,
            "StartPosition" = @StartPosition,
            "EndPosition" = @EndPosition,
            "QuotedText" = @QuotedText,
            {PublicStateOnEdit},
            "UpdatedAt" = now()
        WHERE "Id" = @Id
          AND "UserId" = @UserId
        """;

    internal const string Delete = """
        DELETE FROM "unshackled-word"."Notes"
        WHERE "Id" = @Id
          AND "UserId" = @UserId
        """;

    internal const string Get = $"""
        SELECT {Columns}
        FROM "unshackled-word"."Notes" n
        JOIN "unshackled-word"."Users" u ON u."Id" = n."UserId"
        WHERE n."Id" = @Id
          AND {VisibilityFilter}
        """;

    internal const string ListChapter = $"""
        SELECT {Columns}
        FROM "unshackled-word"."Notes" n
        JOIN "unshackled-word"."Users" u ON u."Id" = n."UserId"
        WHERE n."BibleBookId" = @BookId
          AND n."Chapter" = @Chapter
          AND {VisibilityFilter}
          AND (@Others OR n."UserId" = @ViewerId)
        ORDER BY n."StartHebRefId" NULLS FIRST, n."StartPosition" NULLS FIRST, n."CreatedAt", n."Id"
        """;
}
