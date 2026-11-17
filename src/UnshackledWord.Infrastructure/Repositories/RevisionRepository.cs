using System.Data.Common;
using Dapper;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Features.Revision;

namespace UnshackledWord.Infrastructure.Repositories;

public sealed class RevisionRepository : IRevisionRepository
{
    private readonly IDbReader _dbReader;
    private readonly IDbConnectionFactory _factory;

    public RevisionRepository(IDbReader dbReader, IDbConnectionFactory factory)
    {
        _dbReader = dbReader;
        _factory = factory;
    }

    public Task<string?> GetBaseVerseTextAsync(int hebRefId, CancellationToken token = default) =>
        _dbReader.ReadFirstOrDefaultAsync<string>(RevisionRepositorySql.BaseVerseText, new { HebRefId = hebRefId });

    public async Task<IReadOnlyList<RevisionBaseWordRow>> GetBaseWordsAsync(int hebRefId, CancellationToken token = default) =>
        (await _dbReader.ReadAsListAsync<RevisionBaseWordRow>(RevisionRepositorySql.BaseWords, new { HebRefId = hebRefId })).ToList();

    public async Task<bool> IsWritableLevelAsync(int level, CancellationToken token = default) =>
        await _dbReader.ReadFirstOrDefaultAsync<int?>(RevisionRepositorySql.WritableLevel, new { Level = level }) is not null;

    public Task<RevisionActiveRow?> GetActiveAsync(int hebRefId, int level, CancellationToken token = default) =>
        _dbReader.ReadFirstOrDefaultAsync<RevisionActiveRow>(RevisionRepositorySql.Active, new { HebRefId = hebRefId, Level = level });

    public async Task<SavedRevisionVersion?> SaveVersionAsync(NewRevisionVersion version, CancellationToken token = default)
    {
        using var connection = await _factory.CreateDbConnectionAsync(token);
        using var transaction = connection.BeginTransaction();

        try
        {
            // The row lock serialises concurrent text saves, mapping saves and withdraws of the same verse.
            var live = await connection.QueryFirstOrDefaultAsync<RevisionActiveRow>(RevisionRepositorySql.LockVerse, new { version.HebRefId, version.Level }, transaction);

            if ((live is not null && live.Version != version.ExpectedVersion) || (live is null && version.ExpectedVersion is not null))
            {
                transaction.Rollback();
                return null;
            }

            var saved = live is null
                ? await InsertVerseAsync(connection, transaction, version)
                : await UpdateVerseAsync(connection, transaction, live, version);

            var carried = version.Words.ToDictionary(w => w.Position);
            var wordIds = new Dictionary<int, int>(); // position -> RevisionWords.Id

            foreach (var t in version.Tokens)
            {
                carried.TryGetValue(t.Position, out var word);
                wordIds[t.Position] = await connection.ExecuteScalarAsync<int>(
                    RevisionRepositorySql.InsertWord,
                    new
                    {
                        RevisionVerseId = saved.RevisionVerseId,
                        PositionInVerse = t.Position,
                        t.WordInContext,
                        t.PlainWord,
                        BasePositionInVerse = word?.BasePositionInVerse,
                        Change = word?.Change ?? RevisionCarry.New,
                    },
                    transaction);
            }

            var rows = version.CarriedRows
                .Where(r => wordIds.ContainsKey(r.Position))
                .Select(r => new
                {
                    RevisionWordId = wordIds[r.Position],
                    Lang = version.Lang,
                    r.StepWordId,
                    r.IsAddedWord,
                    ParentRevisionWordId = r.ParentPosition is { } p && wordIds.TryGetValue(p, out var parentId) ? parentId : (int?)null,
                    r.GermanWordPart,
                    Origin = RevisionOrigin.Carried,
                    CreatedByUserId = version.CreatedByUserId,
                    // The carry-over does not transport roles yet (roadmap 0018): carried rows start as head on the main morpheme.
                    Role = "head",
                    StrongsNumber = (string?)null,
                    PartLemma = (string?)null,
                    MultiHead = false,
                })
                .ToList();

            if (rows.Count > 0)
            {
                await connection.ExecuteAsync(RevisionRepositorySql.InsertMapping, rows, transaction);
            }

            await RevisionAuditWriter.WriteAsync(connection, transaction, saved.RevisionVerseId, RevisionAuditAction.Text, version.CreatedByUserId);

            transaction.Commit();
            return saved;
        }
        catch (DbException ex) when (ex.SqlState == UniqueViolation)
        {
            // A concurrent first save of the same (verse, level) won; the caller answers 409.
            transaction.Rollback();
            return null;
        }
    }

    // No live row: a new verse continues from the highest audited version, so a withdrawn verse that is written again does not restart at 1.
    private static async Task<SavedRevisionVersion> InsertVerseAsync(System.Data.IDbConnection connection, System.Data.IDbTransaction transaction, NewRevisionVersion version)
    {
        var next = await connection.ExecuteScalarAsync<int>(RevisionRepositorySql.NextVersion, new { version.HebRefId, version.Level }, transaction);
        var id = await connection.ExecuteScalarAsync<int>(RevisionRepositorySql.InsertVerse, VerseParameters(version, next), transaction);

        return new SavedRevisionVersion(id, next);
    }

    // Live row: update in place, drop the old words (their mapping rows go by cascade; the carry source was read before this call) and reset the review.
    private static async Task<SavedRevisionVersion> UpdateVerseAsync(System.Data.IDbConnection connection, System.Data.IDbTransaction transaction, RevisionActiveRow live, NewRevisionVersion version)
    {
        var next = live.Version + 1;
        await connection.ExecuteAsync(RevisionRepositorySql.UpdateVerse, new { Id = live.Id, version.VerseText, version.BaseTextHash, version.Note, version.BasedOnRevisionId, version.BasedOnVersion, UserId = version.CreatedByUserId, Version = next }, transaction);
        await connection.ExecuteAsync(RevisionRepositorySql.DeleteWords, new { live.Id }, transaction);

        return new SavedRevisionVersion(live.Id, next);
    }

    private static object VerseParameters(NewRevisionVersion version, int next) => new
    {
        version.HebRefId,
        version.Level,
        Version = next,
        version.VerseText,
        version.BaseTextHash,
        version.Note,
        UserId = version.CreatedByUserId,
        version.BasedOnRevisionId,
        version.BasedOnVersion,
    };

    public async Task<IReadOnlyList<RevisionHistoryRow>> GetHistoryAsync(int hebRefId, int level, CancellationToken token = default) =>
        (await _dbReader.ReadAsListAsync<RevisionHistoryRow>(RevisionRepositorySql.History, new { HebRefId = hebRefId, Level = level })).ToList();

    public async Task<bool> EndVariantAsync(int revisionVerseId, int userId, CancellationToken token = default)
    {
        using var connection = await _factory.CreateDbConnectionAsync(token);
        using var transaction = connection.BeginTransaction();

        if (await connection.ExecuteScalarAsync<int?>(RevisionRepositorySql.LockActive, new { Id = revisionVerseId }, transaction) is null)
        {
            transaction.Rollback();
            return false;
        }

        // The audit row copies the live row, so it is written before the delete; the delete takes words and mapping along (cascade).
        await RevisionAuditWriter.WriteAsync(connection, transaction, revisionVerseId, RevisionAuditAction.Withdraw, userId);
        await connection.ExecuteAsync(RevisionRepositorySql.DeleteVerse, new { Id = revisionVerseId }, transaction);
        transaction.Commit();
        return true;
    }

    public async Task<IReadOnlyList<RevisionVariantRow>> GetActiveVariantsAsync(int hebRefId, CancellationToken token = default) =>
        (await _dbReader.ReadAsListAsync<RevisionVariantRow>(RevisionRepositorySql.ActiveVariants, new { HebRefId = hebRefId })).ToList();

    public Task<RevisionVerseHeaderRow?> GetHeaderAsync(int revisionVerseId, CancellationToken token = default) =>
        _dbReader.ReadFirstOrDefaultAsync<RevisionVerseHeaderRow>(RevisionRepositorySql.Header, new { Id = revisionVerseId });

    public async Task<IReadOnlyList<RevisionWordRow>> GetWordsAsync(int revisionVerseId, CancellationToken token = default) =>
        (await _dbReader.ReadAsListAsync<RevisionWordRow>(RevisionRepositorySql.Words, new { Id = revisionVerseId })).ToList();

    public async Task<IReadOnlyList<RevisionMappingRow>> GetMappingAsync(int revisionVerseId, CancellationToken token = default) =>
        (await _dbReader.ReadAsListAsync<RevisionMappingRow>(RevisionRepositorySql.Mapping, new { Id = revisionVerseId })).ToList();

    public async Task<bool> ReplaceMappingAsync(int revisionVerseId, string lang, IReadOnlyList<RevisionMappingRow> rows, int userId, CancellationToken token = default)
    {
        using var connection = await _factory.CreateDbConnectionAsync(token);
        using var transaction = connection.BeginTransaction();

        // The row lock also serialises this save against a concurrent text save or withdraw of the verse.
        var locked = await connection.ExecuteScalarAsync<int?>(RevisionRepositorySql.LockActive, new { Id = revisionVerseId }, transaction);
        if (locked is null)
        {
            transaction.Rollback();
            return false;
        }

        // A text save that ran after the caller read the words replaced them (new ids): the rows would point at deleted words.
        var wordIds = (await connection.QueryAsync<int>(RevisionRepositorySql.WordIds, new { Id = revisionVerseId }, transaction)).ToHashSet();
        if (rows.Any(r => !wordIds.Contains(r.RevisionWordId) || (r.ParentRevisionWordId is { } parent && !wordIds.Contains(parent))))
        {
            transaction.Rollback();
            return false;
        }

        await connection.ExecuteAsync(RevisionRepositorySql.DeleteMapping, new { Id = revisionVerseId }, transaction);

        if (rows.Count > 0)
        {
            await connection.ExecuteAsync(
                RevisionRepositorySql.InsertMapping,
                rows.Select(r => new
                {
                    r.RevisionWordId,
                    Lang = lang,
                    r.StepWordId,
                    r.IsAddedWord,
                    r.ParentRevisionWordId,
                    r.GermanWordPart,
                    r.Origin,
                    CreatedByUserId = (int?)userId,
                    r.Role,
                    StrongsNumber = lang == "H" ? r.StrongsNumber : null,
                    PartLemma = r.GermanWordPart is null ? null : r.PartLemma,
                    r.MultiHead,
                }),
                transaction);
        }

        await connection.ExecuteAsync(RevisionRepositorySql.MarkReviewed, new { Id = revisionVerseId, UserId = userId }, transaction);
        await RevisionAuditWriter.WriteAsync(connection, transaction, revisionVerseId, RevisionAuditAction.Mapping, userId);
        transaction.Commit();
        return true;
    }

    private const string UniqueViolation = "23505";
}

/// <summary>SQL of <see cref="RevisionRepository"/>, public so tests can assert its shape.</summary>
public static class RevisionRepositorySql
{
    public const string BaseVerseText = """
                                        SELECT v."VerseText"
                                        FROM "unshackled-word"."Elb1871Verses" v
                                        WHERE v."HebRefId" = @HebRefId
                                        """;

    public const string BaseWords = """
                                    SELECT w."PositionInVerse"
                                         , w."PlainWord"
                                    FROM "unshackled-word"."Elb1871Words" w
                                    WHERE w."HebRefId" = @HebRefId
                                    ORDER BY w."PositionInVerse"
                                           , w."Id"
                                    """;

    public const string WritableLevel = """
                                        SELECT l."Level"
                                        FROM "unshackled-word"."Revisions" l
                                        WHERE l."Level" = @Level
                                            AND NOT l."IsBase"
                                        """;

    public const string Active = """
                                 SELECT rv."Id"
                                      , rv."Version"
                                      , rv."RevisionId"
                                 FROM "unshackled-word"."RevisionVerses" rv
                                 WHERE rv."HebRefId" = @HebRefId
                                     AND rv."RevisionId" = (SELECT r."Id" FROM "unshackled-word"."Revisions" r WHERE r."Level" = @Level AND NOT r."IsBase")
                                 """;

    /// <summary>Same row as <see cref="Active"/>, locked for the text save.</summary>
    public const string LockVerse = Active + """

                                     FOR UPDATE
                                     """;

    /// <summary>Next version of a verse without a live row: the highest audited version + 1 (1 for a verse that never existed).</summary>
    public const string NextVersion = """
                                      SELECT COALESCE(max(a."Version"), 0) + 1
                                      FROM "unshackled-word"."RevisionVerseAudit" a
                                      WHERE a."HebRefId" = @HebRefId
                                          AND a."RevisionId" = (SELECT r."Id" FROM "unshackled-word"."Revisions" r WHERE r."Level" = @Level AND NOT r."IsBase")
                                      """;

    public const string InsertVerse = """
                                      INSERT INTO "unshackled-word"."RevisionVerses"
                                          ("HebRefId", "RevisionId", "Version", "VerseText", "BaseTextHash", "Note", "CreatedByUserId", "UpdatedByUserId", "BasedOnRevisionId", "BasedOnVersion")
                                      VALUES (@HebRefId, (SELECT r."Id" FROM "unshackled-word"."Revisions" r WHERE r."Level" = @Level AND NOT r."IsBase"), @Version, @VerseText, @BaseTextHash, @Note, @UserId, @UserId, @BasedOnRevisionId, @BasedOnVersion)
                                      RETURNING "Id"
                                      """;

    public const string UpdateVerse = """
                                      UPDATE "unshackled-word"."RevisionVerses"
                                      SET "Version"                 = @Version
                                        , "VerseText"               = @VerseText
                                        , "BaseTextHash"            = @BaseTextHash
                                        , "Note"                    = @Note
                                        , "BasedOnRevisionId"       = @BasedOnRevisionId
                                        , "BasedOnVersion"          = @BasedOnVersion
                                        , "UpdatedByUserId"         = @UserId
                                        , "UpdatedAt"               = now()
                                        , "MappingReviewedAt"       = NULL
                                        , "MappingReviewedByUserId" = NULL
                                      WHERE "Id" = @Id
                                      """;

    /// <summary>Deletes the words of a verse; their mapping rows go with them (cascade).</summary>
    public const string DeleteWords = """
                                      DELETE
                                      FROM "unshackled-word"."RevisionWords"
                                      WHERE "RevisionVerseId" = @Id
                                      """;

    /// <summary>Deletes a verse for a withdraw; words and mapping go with it (cascade).</summary>
    public const string DeleteVerse = """
                                      DELETE
                                      FROM "unshackled-word"."RevisionVerses"
                                      WHERE "Id" = @Id
                                      """;

    public const string WordIds = """
                                  SELECT rw."Id"
                                  FROM "unshackled-word"."RevisionWords" rw
                                  WHERE rw."RevisionVerseId" = @Id
                                  """;

    public const string InsertWord = """
                                     INSERT INTO "unshackled-word"."RevisionWords" ("RevisionVerseId", "PositionInVerse", "WordInContext", "PlainWord", "BasePositionInVerse", "Change")
                                     VALUES (@RevisionVerseId, @PositionInVerse, @WordInContext, @PlainWord, @BasePositionInVerse, @Change)
                                     RETURNING "Id"
                                     """;

    public const string InsertMapping = """
                                        INSERT INTO "unshackled-word"."RevisionMapping"
                                            ("RevisionWordId", "Lang", "StepWordId", "IsAddedWord", "ParentRevisionWordId", "GermanWordPart", "Origin", "CreatedByUserId", "Role", "StrongsNumber", "PartLemma", "MultiHead")
                                        VALUES (@RevisionWordId, @Lang, @StepWordId, @IsAddedWord, @ParentRevisionWordId, @GermanWordPart, @Origin, @CreatedByUserId, @Role, @StrongsNumber, @PartLemma, @MultiHead)
                                        """;

    public const string Header = """
                                 SELECT rv."Id"
                                      , rv."HebRefId"
                                      , rev."Level"
                                      , rv."Version"
                                      , rv."MappingReviewedAt"
                                 FROM "unshackled-word"."RevisionVerses" rv
                                     JOIN "unshackled-word"."Revisions" rev ON rev."Id" = rv."RevisionId"
                                 WHERE rv."Id" = @Id
                                 """;

    public const string Words = """
                                SELECT rw."Id"
                                     , rw."PositionInVerse"
                                     , rw."WordInContext"
                                     , rw."PlainWord"
                                     , rw."BasePositionInVerse"
                                     , rw."Change"
                                FROM "unshackled-word"."RevisionWords" rw
                                WHERE rw."RevisionVerseId" = @Id
                                ORDER BY rw."PositionInVerse"
                                """;

    public const string Mapping = """
                                  SELECT rm."RevisionWordId"
                                       , rm."StepWordId"
                                       , rm."IsAddedWord"
                                       , rm."ParentRevisionWordId"
                                       , rm."GermanWordPart"
                                       , rm."Origin"
                                       , rm."Role"
                                       , rm."StrongsNumber"
                                       , rm."PartLemma"
                                       , rm."MultiHead"
                                  FROM "unshackled-word"."RevisionMapping" rm
                                      JOIN "unshackled-word"."RevisionWords" rw ON rw."Id" = rm."RevisionWordId"
                                  WHERE rw."RevisionVerseId" = @Id
                                  ORDER BY rw."PositionInVerse"
                                         , rm."Id"
                                  """;

    public const string LockActive = """
                                     SELECT rv."Id"
                                     FROM "unshackled-word"."RevisionVerses" rv
                                     WHERE rv."Id" = @Id
                                     FOR UPDATE
                                     """;

    public const string DeleteMapping = """
                                        DELETE
                                        FROM "unshackled-word"."RevisionMapping" rm
                                        USING "unshackled-word"."RevisionWords" rw
                                        WHERE rw."Id" = rm."RevisionWordId"
                                            AND rw."RevisionVerseId" = @Id
                                        """;

    public const string MarkReviewed = """
                                       UPDATE "unshackled-word"."RevisionVerses"
                                       SET "MappingReviewedAt"       = now()
                                         , "MappingReviewedByUserId" = @UserId
                                       WHERE "Id" = @Id
                                       """;

    public const string History = """
                                  SELECT a."Id"          AS "AuditId"
                                       , a."Version"     AS "Version"
                                       , a."Action"      AS "Action"
                                       , a."VerseText"   AS "VerseText"
                                       , a."Note"        AS "Note"
                                       , a."At"          AS "At"
                                       , u."DisplayName" AS "By"
                                  FROM "unshackled-word"."RevisionVerseAudit" a
                                      LEFT JOIN "unshackled-word"."Users" u ON u."Id" = a."ByUserId"
                                  WHERE a."HebRefId" = @HebRefId
                                      AND a."RevisionId" = (SELECT r."Id" FROM "unshackled-word"."Revisions" r WHERE r."Level" = @Level AND NOT r."IsBase")
                                  ORDER BY a."Id" DESC
                                  """;

    public const string ActiveVariants = """
                                         SELECT rv."Id"                  AS "RevisionVerseId"
                                              , rev."Level"              AS "Level"
                                              , rv."Version"             AS "Version"
                                              , rv."VerseText"           AS "VerseText"
                                              , rv."UpdatedAt"           AS "CreatedAt"
                                              , u."DisplayName"          AS "CreatedBy"
                                              , rv."MappingReviewedAt"   AS "MappingReviewedAt"
                                         FROM "unshackled-word"."RevisionVerses" rv
                                             JOIN "unshackled-word"."Revisions" rev ON rev."Id" = rv."RevisionId"
                                             LEFT JOIN "unshackled-word"."Users" u ON u."Id" = rv."UpdatedByUserId"
                                         WHERE rv."HebRefId" = @HebRefId
                                         ORDER BY rev."Level"
                                         """;
}
