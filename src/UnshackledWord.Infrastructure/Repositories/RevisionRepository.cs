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

    public async Task<int> GetLastVersionAsync(int hebRefId, int level, CancellationToken token = default) =>
        await _dbReader.ExecuteScalarAsync<int?>(RevisionRepositorySql.LastVersion, new { HebRefId = hebRefId, Level = level }) ?? 0;

    public async Task<int?> InsertVersionAsync(NewRevisionVersion version, int endedByUserId, CancellationToken token = default)
    {
        using var connection = await _factory.CreateDbConnectionAsync(token);
        using var transaction = connection.BeginTransaction();

        try
        {
            if (version.EndActiveRevisionVerseId is { } activeId)
            {
                await connection.ExecuteAsync(RevisionRepositorySql.EndActive, new { Id = activeId, EndedByUserId = endedByUserId }, transaction);
            }

            var id = await connection.ExecuteScalarAsync<int>(RevisionRepositorySql.InsertVerse, version, transaction);
            var carried = version.Words.ToDictionary(w => w.Position);
            var wordIds = new Dictionary<int, int>(); // position -> RevisionWords.Id

            foreach (var t in version.Tokens)
            {
                carried.TryGetValue(t.Position, out var word);
                wordIds[t.Position] = await connection.ExecuteScalarAsync<int>(
                    RevisionRepositorySql.InsertWord,
                    new
                    {
                        RevisionVerseId = id,
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
                })
                .ToList();

            if (rows.Count > 0)
            {
                await connection.ExecuteAsync(RevisionRepositorySql.InsertMapping, rows, transaction);
            }

            transaction.Commit();
            return id;
        }
        catch (DbException ex) when (ex.SqlState == UniqueViolation)
        {
            // A concurrent save of the same (verse, level) won; the caller answers 409.
            transaction.Rollback();
            return null;
        }
    }

    public async Task<IReadOnlyList<RevisionHistoryRow>> GetHistoryAsync(int hebRefId, int level, CancellationToken token = default) =>
        (await _dbReader.ReadAsListAsync<RevisionHistoryRow>(RevisionRepositorySql.History, new { HebRefId = hebRefId, Level = level })).ToList();

    public async Task<bool> EndVariantAsync(int revisionVerseId, int userId, CancellationToken token = default)
    {
        using var connection = await _factory.CreateDbConnectionAsync(token);
        return await connection.ExecuteAsync(RevisionRepositorySql.EndActive, new { Id = revisionVerseId, EndedByUserId = userId }) == 1;
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

        // The row lock also serialises this save against a concurrent text save that ends the variant.
        var active = await connection.ExecuteScalarAsync<int?>(RevisionRepositorySql.LockActive, new { Id = revisionVerseId }, transaction);
        if (active is null)
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
                }),
                transaction);
        }

        await connection.ExecuteAsync(RevisionRepositorySql.MarkReviewed, new { Id = revisionVerseId, UserId = userId }, transaction);
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
                                 FROM "unshackled-word"."RevisionVerses" rv
                                 WHERE rv."HebRefId" = @HebRefId
                                     AND rv."RevisionId" = (SELECT r."Id" FROM "unshackled-word"."Revisions" r WHERE r."Level" = @Level AND NOT r."IsBase")
                                     AND rv."IsActive"
                                 """;

    public const string LastVersion = """
                                      SELECT max(rv."Version")
                                      FROM "unshackled-word"."RevisionVerses" rv
                                      WHERE rv."HebRefId" = @HebRefId
                                          AND rv."RevisionId" = (SELECT r."Id" FROM "unshackled-word"."Revisions" r WHERE r."Level" = @Level AND NOT r."IsBase")
                                      """;

    public const string EndActive = """
                                    UPDATE "unshackled-word"."RevisionVerses"
                                    SET "IsActive"      = false
                                      , "EndedAt"       = now()
                                      , "EndedByUserId" = @EndedByUserId
                                    WHERE "Id" = @Id
                                        AND "IsActive"
                                    """;

    public const string InsertVerse = """
                                      INSERT INTO "unshackled-word"."RevisionVerses" ("HebRefId", "RevisionId", "Version", "VerseText", "BaseTextHash", "Note", "CreatedByUserId", "BasedOnRevisionVerseId")
                                      VALUES (@HebRefId, (SELECT r."Id" FROM "unshackled-word"."Revisions" r WHERE r."Level" = @Level AND NOT r."IsBase"), @Version, @VerseText, @BaseTextHash, @Note, @CreatedByUserId, @BasedOnRevisionVerseId)
                                      RETURNING "Id"
                                      """;

    public const string InsertWord = """
                                     INSERT INTO "unshackled-word"."RevisionWords" ("RevisionVerseId", "PositionInVerse", "WordInContext", "PlainWord", "BasePositionInVerse", "Change")
                                     VALUES (@RevisionVerseId, @PositionInVerse, @WordInContext, @PlainWord, @BasePositionInVerse, @Change)
                                     RETURNING "Id"
                                     """;

    public const string InsertMapping = """
                                        INSERT INTO "unshackled-word"."RevisionMapping"
                                            ("RevisionWordId", "Lang", "StepWordId", "IsAddedWord", "ParentRevisionWordId", "GermanWordPart", "Origin", "CreatedByUserId")
                                        VALUES (@RevisionWordId, @Lang, @StepWordId, @IsAddedWord, @ParentRevisionWordId, @GermanWordPart, @Origin, @CreatedByUserId)
                                        """;

    public const string Header = """
                                 SELECT rv."Id"
                                      , rv."HebRefId"
                                      , rev."Level"
                                      , rv."Version"
                                      , rv."IsActive"
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
                                         AND rv."IsActive"
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
                                  SELECT rv."Id"          AS "RevisionVerseId"
                                       , rv."Version"     AS "Version"
                                       , rv."VerseText"   AS "VerseText"
                                       , rv."Note"        AS "Note"
                                       , rv."IsActive"    AS "IsActive"
                                       , rv."CreatedAt"   AS "CreatedAt"
                                       , u."DisplayName"  AS "CreatedBy"
                                       , rv."EndedAt"     AS "EndedAt"
                                  FROM "unshackled-word"."RevisionVerses" rv
                                      LEFT JOIN "unshackled-word"."Users" u ON u."Id" = rv."CreatedByUserId"
                                  WHERE rv."HebRefId" = @HebRefId
                                      AND rv."RevisionId" = (SELECT r."Id" FROM "unshackled-word"."Revisions" r WHERE r."Level" = @Level AND NOT r."IsBase")
                                  ORDER BY rv."Version" DESC
                                  """;

    public const string ActiveVariants = """
                                         SELECT rv."Id"                  AS "RevisionVerseId"
                                              , rev."Level"              AS "Level"
                                              , rv."Version"             AS "Version"
                                              , rv."VerseText"           AS "VerseText"
                                              , rv."CreatedAt"           AS "CreatedAt"
                                              , u."DisplayName"          AS "CreatedBy"
                                              , rv."MappingReviewedAt"   AS "MappingReviewedAt"
                                         FROM "unshackled-word"."RevisionVerses" rv
                                             JOIN "unshackled-word"."Revisions" rev ON rev."Id" = rv."RevisionId"
                                             LEFT JOIN "unshackled-word"."Users" u ON u."Id" = rv."CreatedByUserId"
                                         WHERE rv."HebRefId" = @HebRefId
                                             AND rv."IsActive"
                                         ORDER BY rev."Level"
                                         """;
}
