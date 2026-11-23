using System.Data;
using Dapper;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Features.Notes;
using UnshackledWord.Application.Features.Revision;

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

    public async Task<IReadOnlyList<NoteWordRow>> GetElbWordsAsync(IReadOnlyCollection<int> hebRefIds, CancellationToken token = default)
    {
        using var connection = await _factory.CreateDbConnectionAsync(token);

        return await GetElbWordsAsync(connection, null, hebRefIds, token);
    }

    public async Task<IReadOnlyList<NoteVariantWordRow>> GetVariantWordsAsync(IReadOnlyCollection<int> hebRefIds, int level, CancellationToken token = default)
    {
        using var connection = await _factory.CreateDbConnectionAsync(token);

        return await GetVariantWordsAsync(connection, null, hebRefIds, level, token);
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

    public async Task<NotePage> ListMineAsync(int userId, NoteListFilter filter, CancellationToken token = default)
    {
        using var connection = await _factory.CreateDbConnectionAsync(token);

        return await ListMineAsync(connection, null, userId, filter, token);
    }

    public async Task<IReadOnlyList<NoteExportRow>> ListForExportAsync(int? userId, CancellationToken token = default)
    {
        using var connection = await _factory.CreateDbConnectionAsync(token);

        return await ListForExportAsync(connection, null, userId, token);
    }

    public async Task<bool> SetVisibilityAsync(Guid id, int userId, string visibility, CancellationToken token = default)
    {
        using var connection = await _factory.CreateDbConnectionAsync(token);

        return await SetVisibilityAsync(connection, null, id, userId, visibility, token);
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken token = default)
    {
        using var connection = await _factory.CreateDbConnectionAsync(token);

        return await ExistsAsync(connection, null, id, token);
    }

    public static Task<bool> ExistsAsync(IDbConnection c, IDbTransaction? t, Guid id, CancellationToken token = default) =>
        c.ExecuteScalarAsync<bool>(new CommandDefinition(NoteSql.Exists, new { Id = id }, t, cancellationToken: token));

    public async Task<int?> ReviewAsync(Guid id, NoteReviewAction action, int actorUserId, string? reason, CancellationToken token = default)
    {
        using var connection = await _factory.CreateDbConnectionAsync(token);

        return await ReviewAsync(connection, null, id, action, actorUserId, reason, token);
    }

    public async Task<NoteReviewQueue> ListReviewAsync(int viewerId, CancellationToken token = default)
    {
        using var connection = await _factory.CreateDbConnectionAsync(token);

        return await ListReviewAsync(connection, null, viewerId, token);
    }

    public static async Task<bool> SetVisibilityAsync(IDbConnection c, IDbTransaction? t, Guid id, int userId, string visibility, CancellationToken token = default) =>
        await c.ExecuteAsync(new CommandDefinition(NoteSql.SetVisibility, new { Id = id, UserId = userId, Visibility = visibility }, t, cancellationToken: token)) == 1;

    public static Task<int?> ReviewAsync(IDbConnection c, IDbTransaction? t, Guid id, NoteReviewAction action, int actorUserId, string? reason, CancellationToken token = default)
    {
        var sql = action switch
        {
            NoteReviewAction.Approve => NoteSql.Approve,
            NoteReviewAction.Reject => NoteSql.Reject,
            NoteReviewAction.Hide => NoteSql.Hide,
            NoteReviewAction.Unhide => NoteSql.Unhide,
            _ => throw new ArgumentOutOfRangeException(nameof(action)),
        };

        return c.QuerySingleOrDefaultAsync<int?>(new CommandDefinition(sql, new { Id = id, ActorId = actorUserId, Reason = reason }, t, cancellationToken: token));
    }

    public static async Task<NoteReviewQueue> ListReviewAsync(IDbConnection c, IDbTransaction? t, int viewerId, CancellationToken token = default)
    {
        using var multi = await c.QueryMultipleAsync(new CommandDefinition(NoteSql.ListReview, new { ViewerId = viewerId }, t, cancellationToken: token));
        var pending = (await multi.ReadAsync<NoteRecord>()).ToList();
        var hidden = (await multi.ReadAsync<NoteRecord>()).ToList();

        return new NoteReviewQueue(pending, hidden);
    }

    public static async Task<NotePage> ListMineAsync(IDbConnection c, IDbTransaction? t, int userId, NoteListFilter filter, CancellationToken token = default)
    {
        var parameters = new
        {
            UserId = userId,
            ViewerId = userId,
            BookId = filter.BookId,
            filter.Visibility,
            Pattern = string.IsNullOrWhiteSpace(filter.Query) ? null : NoteSearch.ContainsPattern(filter.Query.Trim()),
            filter.Skip,
            filter.Take,
        };
        using var multi = await c.QueryMultipleAsync(new CommandDefinition(NoteSql.ListMine, parameters, t, cancellationToken: token));
        var total = await multi.ReadSingleAsync<int>();
        var rows = (await multi.ReadAsync<NoteRecord>()).ToList();

        return new NotePage(total, rows);
    }

    public static async Task<IReadOnlyList<NoteExportRow>> ListForExportAsync(IDbConnection c, IDbTransaction? t, int? userId, CancellationToken token = default) =>
        (await c.QueryAsync<NoteExportRow>(new CommandDefinition(NoteSql.ListForExport, new { UserId = userId }, t, cancellationToken: token))).ToList();

    public static Task<bool> VerseExistsAsync(IDbConnection c, IDbTransaction? t, int hebRefId, CancellationToken token = default) =>
        c.ExecuteScalarAsync<bool>(new CommandDefinition(NoteSql.VerseExists, new { HebRefId = hebRefId }, t, cancellationToken: token));

    public static Task<bool> ChapterExistsAsync(IDbConnection c, IDbTransaction? t, int bookId, int chapter, CancellationToken token = default) =>
        c.ExecuteScalarAsync<bool>(new CommandDefinition(NoteSql.ChapterExists, new
        {
            From = bookId * 1_000_000 + chapter * 1_000,
            To = bookId * 1_000_000 + chapter * 1_000 + 999,
        }, t, cancellationToken: token));

    public static async Task<IReadOnlyList<NoteWordRow>> GetElbWordsAsync(IDbConnection c, IDbTransaction? t, IReadOnlyCollection<int> hebRefIds, CancellationToken token = default) =>
        (await c.QueryAsync<NoteWordRow>(new CommandDefinition(NoteSql.ElbWords, new { Ids = hebRefIds.ToArray() }, t, cancellationToken: token))).ToList();

    public static async Task<IReadOnlyList<NoteVariantWordRow>> GetVariantWordsAsync(IDbConnection c, IDbTransaction? t, IReadOnlyCollection<int> hebRefIds, int level, CancellationToken token = default)
    {
        if (hebRefIds.Count == 0)
        {
            return [];
        }

        return (await c.QueryAsync<NoteVariantWordRow>(new CommandDefinition(NoteSql.VariantWords, new
        {
            Ids = hebRefIds.ToArray(),
            Level = level,
            FromRefId = hebRefIds.Min(),
            ToRefId = hebRefIds.Max(),
        }, t, cancellationToken: token))).ToList();
    }

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
               n."ApprovedBodyMarkdown"                                                      AS "ApprovedBodyMarkdown",
               n."ApprovedAt"                                                                AS "ApprovedAt",
               n."HiddenAt"                                                                  AS "HiddenAt",
               n."ReviewNote"                                                                AS "ReviewNote",
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

    internal const string ElbWords = """
        SELECT w."HebRefId"        AS "HebRefId",
               w."PositionInVerse" AS "PositionInVerse",
               w."WordInContext"   AS "WordInContext"
        FROM "unshackled-word"."Elb1871Words" w
        WHERE w."HebRefId" = ANY(@Ids)
        ORDER BY w."HebRefId", w."PositionInVerse"
        """;

    /// <summary>The variant the reader shows at @Level (same selection as the chapter endpoints) with its words; verses without a variant return nothing.</summary>
    internal const string VariantWords = $"""
        WITH ev AS ({RevisionSql.EffectiveVerses})
        SELECT ev."HebRefId"             AS "HebRefId",
               ev."RevisionVerseId"      AS "RevisionVerseId",
               rw."Id"                   AS "RevisionWordId",
               rw."PositionInVerse"      AS "PositionInVerse",
               rw."BasePositionInVerse"  AS "BasePositionInVerse"
        FROM ev
        JOIN "unshackled-word"."RevisionWords" rw ON rw."RevisionVerseId" = ev."RevisionVerseId"
        WHERE ev."RevisionVerseId" IS NOT NULL
          AND ev."HebRefId" = ANY(@Ids)
        ORDER BY ev."HebRefId", rw."PositionInVerse"
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

    internal const string Exists = """
        SELECT EXISTS (SELECT 1
                       FROM "unshackled-word"."Notes" n
                       WHERE n."Id" = @Id)
        """;

    /// <summary>
    /// private/internal: back to no review state and no approval snapshot. public: requests review; a pending or approved note keeps its state.
    /// A reason (rejection or hide) stays while the note is hidden. All expressions see the old row.
    /// </summary>
    internal const string SetVisibility = """
        UPDATE "unshackled-word"."Notes"
        SET "Visibility" = @Visibility,
            "PublicState" = CASE
                                WHEN @Visibility <> 'public' THEN 'none'
                                WHEN "PublicState" IN ('none', 'rejected') THEN 'pending'
                                ELSE "PublicState"
                            END,
            "ApprovedBodyMarkdown" = CASE WHEN @Visibility <> 'public' THEN NULL ELSE "ApprovedBodyMarkdown" END,
            "ApprovedAt" = CASE WHEN @Visibility <> 'public' THEN NULL ELSE "ApprovedAt" END,
            "ApprovedByUserId" = CASE WHEN @Visibility <> 'public' THEN NULL ELSE "ApprovedByUserId" END,
            "ReviewNote" = CASE WHEN "HiddenAt" IS NULL AND (@Visibility <> 'public' OR "PublicState" IN ('none', 'rejected')) THEN NULL ELSE "ReviewNote" END
        WHERE "Id" = @Id
          AND "UserId" = @UserId
        """;

    /// <summary>The maintainer approves the text as it is now; anonymous readers see exactly this snapshot from here on.</summary>
    internal const string Approve = """
        UPDATE "unshackled-word"."Notes"
        SET "PublicState" = 'approved',
            "ApprovedBodyMarkdown" = "BodyMarkdown",
            "ApprovedAt" = now(),
            "ApprovedByUserId" = @ActorId,
            "ReviewNote" = CASE WHEN "HiddenAt" IS NULL THEN NULL ELSE "ReviewNote" END
        WHERE "Id" = @Id
          AND "Visibility" = 'public'
          AND "PublicState" = 'pending'
        RETURNING "UserId"
        """;

    /// <summary>A rejection also withdraws an older approval: the note is internal for everybody until the author requests public again.</summary>
    internal const string Reject = """
        UPDATE "unshackled-word"."Notes"
        SET "PublicState" = 'rejected',
            "ApprovedBodyMarkdown" = NULL,
            "ApprovedAt" = NULL,
            "ApprovedByUserId" = NULL,
            "ReviewNote" = @Reason
        WHERE "Id" = @Id
          AND "Visibility" = 'public'
          AND "PublicState" = 'pending'
        RETURNING "UserId"
        """;

    internal const string Hide = """
        UPDATE "unshackled-word"."Notes"
        SET "HiddenAt" = now(),
            "HiddenByUserId" = @ActorId,
            "ReviewNote" = @Reason
        WHERE "Id" = @Id
          AND "Visibility" <> 'private'
          AND "HiddenAt" IS NULL
        RETURNING "UserId"
        """;

    /// <summary>A rejection reason survives the hide/unhide round trip only if it was not overwritten by the hide reason.</summary>
    internal const string Unhide = """
        UPDATE "unshackled-word"."Notes"
        SET "HiddenAt" = NULL,
            "HiddenByUserId" = NULL,
            "ReviewNote" = CASE WHEN "PublicState" = 'rejected' THEN "ReviewNote" ELSE NULL END
        WHERE "Id" = @Id
          AND "HiddenAt" IS NOT NULL
        RETURNING "UserId"
        """;

    /// <summary>Two result sets: pending public notes that are not hidden, then every hidden note; oldest change first.</summary>
    internal const string ListReview = $"""
        SELECT {Columns}
        FROM "unshackled-word"."Notes" n
        JOIN "unshackled-word"."Users" u ON u."Id" = n."UserId"
        WHERE n."Visibility" = 'public'
          AND n."PublicState" = 'pending'
          AND n."HiddenAt" IS NULL
        ORDER BY COALESCE(n."UpdatedAt", n."CreatedAt"), n."Id";

        SELECT {Columns}
        FROM "unshackled-word"."Notes" n
        JOIN "unshackled-word"."Users" u ON u."Id" = n."UserId"
        WHERE n."HiddenAt" IS NOT NULL
        ORDER BY n."HiddenAt", n."Id";
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

    /// <summary>The caller's own notes, all visibilities (own hidden notes too); filters are optional. Two result sets: total, then the page.</summary>
    internal const string ListMine = $"""
        SELECT count(*)::int4
        FROM "unshackled-word"."Notes" n
        WHERE n."UserId" = @UserId
          AND (@BookId::int4 IS NULL OR n."BibleBookId" = @BookId)
          AND (@Visibility::text IS NULL OR n."Visibility" = @Visibility)
          AND (@Pattern::text IS NULL OR n."BodyMarkdown" ILIKE @Pattern ESCAPE '\');

        SELECT {Columns}
        FROM "unshackled-word"."Notes" n
        JOIN "unshackled-word"."Users" u ON u."Id" = n."UserId"
        WHERE n."UserId" = @UserId
          AND (@BookId::int4 IS NULL OR n."BibleBookId" = @BookId)
          AND (@Visibility::text IS NULL OR n."Visibility" = @Visibility)
          AND (@Pattern::text IS NULL OR n."BodyMarkdown" ILIKE @Pattern ESCAPE '\')
        ORDER BY COALESCE(n."UpdatedAt", n."CreatedAt") DESC, n."Id"
        OFFSET @Skip LIMIT @Take;
        """;

    internal const string ListForExport = """
        SELECT n."Id"                   AS "Id",
               u."Email"                AS "AuthorEmail",
               u."DisplayName"          AS "AuthorDisplayName",
               n."Kind"                 AS "Kind",
               n."BibleBookId"          AS "BibleBookId",
               n."Chapter"              AS "Chapter",
               n."StartHebRefId"        AS "StartHebRefId",
               n."EndHebRefId"          AS "EndHebRefId",
               n."StartPosition"        AS "StartPosition",
               n."EndPosition"          AS "EndPosition",
               n."QuotedText"           AS "QuotedText",
               n."BodyMarkdown"         AS "BodyMarkdown",
               n."Visibility"           AS "Visibility",
               n."PublicState"          AS "PublicState",
               n."ApprovedBodyMarkdown" AS "ApprovedBodyMarkdown",
               n."ApprovedAt"           AS "ApprovedAt",
               a."Email"                AS "ApprovedByEmail",
               n."ReviewNote"           AS "ReviewNote",
               n."HiddenAt"             AS "HiddenAt",
               n."CreatedAt"            AS "CreatedAt",
               n."UpdatedAt"            AS "UpdatedAt"
        FROM "unshackled-word"."Notes" n
        JOIN "unshackled-word"."Users" u ON u."Id" = n."UserId"
        LEFT JOIN "unshackled-word"."Users" a ON a."Id" = n."ApprovedByUserId"
        WHERE @UserId::int4 IS NULL OR n."UserId" = @UserId
        ORDER BY n."Id"
        """;
}
