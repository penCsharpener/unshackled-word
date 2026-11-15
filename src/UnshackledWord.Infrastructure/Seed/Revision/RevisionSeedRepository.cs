using System.Data;
using Dapper;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Infrastructure.Repositories;

namespace UnshackledWord.Infrastructure.Seed.Revision;

/// <summary>What <c>SeedImports</c> records about one revision import or update.</summary>
public sealed record RevisionImportRecord(
    string SeedVersion,
    DateTimeOffset? ExportedAt,
    int Rows,
    int Exact,
    int Shifted,
    int Modified,
    int Orphaned,
    int? VersesUpdated,
    int? VersesKeptLocal);

public sealed class RevisionStateRow
{
    public string Kind { get; init; } = string.Empty;
    public int HebRefId { get; init; }
    public string ContentHash { get; init; } = string.Empty;
}

public sealed class RevisionLatestImportRow
{
    public string SeedVersion { get; init; } = string.Empty;
    public DateTimeOffset ImportedAt { get; init; }
}

/// <summary>A variant ready to be written: verse row, words with their base links and the placed mapping rows (all keyed by position, ids come from the insert).</summary>
public sealed class RevisionVariantWrite
{
    public required int HebRefId { get; init; }
    public required int Level { get; init; }
    public required string VerseText { get; init; }
    public required string BaseHash { get; init; }
    public string? Note { get; init; }

    /// <summary>Level of the revision this text was started from (seed column <c>BasedOnLevel</c>); resolved to revision and version at write time.</summary>
    public int? ParentLevel { get; init; }
    public bool Reviewed { get; init; }
    public required string Lang { get; init; }
    public required IReadOnlyList<RevisionWordWrite> Words { get; init; }
    public required IReadOnlyList<RevisionMappingWrite> Mapping { get; init; }
}

public sealed record RevisionWordWrite(int Position, string WordInContext, string? PlainWord, int? BasePosition, string Change);

public sealed record RevisionMappingWrite(int Position, int? StepWordId, bool Added, int? ParentPosition, string? Part, string Origin);

/// <summary>Database access of the revision seed import and update (tables <c>RevisionVerses</c>, <c>RevisionWords</c>, <c>RevisionMapping</c>).</summary>
public sealed class RevisionSeedRepository
{
    public const string Kind = "revision";
    public const string StateKindPrefix = "revision-";

    private readonly IDbReader _dbReader;
    private readonly IDbConnectionFactory _connectionFactory;

    public RevisionSeedRepository(IDbReader dbReader, IDbConnectionFactory connectionFactory)
    {
        _dbReader = dbReader;
        _connectionFactory = connectionFactory;
    }

    private const string CountVerses = """
        SELECT count(*)
        FROM "unshackled-word"."RevisionVerses"
        """;

    private const string SelectLevels = """
        SELECT l."Level"
        FROM "unshackled-word"."Revisions" l
        WHERE NOT l."IsBase"
        """;

    private const string SelectLatestImport = """
        SELECT i."SeedVersion" AS "SeedVersion"
             , i."ImportedAt"  AS "ImportedAt"
        FROM "unshackled-word"."SeedImports" i
        WHERE i."Kind" = @Kind
        ORDER BY i."ImportedAt" DESC
               , i."Id" DESC
        LIMIT 1
        """;

    private const string SelectState = """
        SELECT s."Kind"        AS "Kind"
             , s."HebRefId"    AS "HebRefId"
             , s."ContentHash" AS "ContentHash"
        FROM "unshackled-word"."SeedVerseState" s
        WHERE s."Kind" LIKE 'revision-%'
        """;

    private const string SelectVerseRefs = """
        SELECT v."HebRefId"
        FROM "unshackled-word"."Elb1871Verses" v
        WHERE v."HebRefId" BETWEEN @FromRefId AND @ToRefId
        """;

    public async Task<int> CountVariantRowsAsync()
    {
        return await _dbReader.ExecuteScalarAsync<int>(CountVerses);
    }

    public async Task<HashSet<int>> ReadLevelsAsync()
    {
        return (await _dbReader.ReadAsListAsync<int>(SelectLevels)).ToHashSet();
    }

    public async Task<RevisionLatestImportRow?> ReadLatestImportAsync()
    {
        return await _dbReader.ReadFirstOrDefaultAsync<RevisionLatestImportRow>(SelectLatestImport, new { Kind });
    }

    /// <summary>The stored hash per (verse, level); the level is the number behind <c>revision-</c> in the state kind.</summary>
    public async Task<Dictionary<(int Ref, int Level), string>> ReadStateAsync()
    {
        var rows = await _dbReader.ReadAsListAsync<RevisionStateRow>(SelectState);

        return rows.ToDictionary(r => (r.HebRefId, int.Parse(r.Kind[StateKindPrefix.Length..], System.Globalization.CultureInfo.InvariantCulture)), r => r.ContentHash);
    }

    public async Task<HashSet<int>> ReadVerseRefsAsync(int bookId)
    {
        return (await _dbReader.ReadAsListAsync<int>(SelectVerseRefs, new { FromRefId = bookId * 1_000_000, ToRefId = bookId * 1_000_000 + 999_999 })).ToHashSet();
    }

    /// <summary>Opens the single transaction of an import or update; the revision tables are locked so the editor and a second seed container cannot write meanwhile.</summary>
    public async Task<RevisionWriteSession> BeginWriteAsync(CancellationToken token)
    {
        var connection = await _connectionFactory.CreateDbConnectionAsync(token);
        var transaction = connection.BeginTransaction();

        await connection.ExecuteAsync(
            """LOCK TABLE "unshackled-word"."RevisionVerses", "unshackled-word"."RevisionWords", "unshackled-word"."RevisionMapping" IN EXCLUSIVE MODE""",
            transaction: transaction);

        return new RevisionWriteSession(connection, transaction);
    }
}

/// <summary>One import or update transaction: variants, verse state and the <c>SeedImports</c> row commit together or not at all.</summary>
public sealed class RevisionWriteSession : IDisposable
{
    private const int CommandTimeoutSeconds = 900;

    private const string LiveRow = """
        SELECT rv."Id"
             , rv."Version"
        FROM "unshackled-word"."RevisionVerses" rv
        WHERE rv."HebRefId" = @HebRefId
            AND rv."RevisionId" = (SELECT r."Id" FROM "unshackled-word"."Revisions" r WHERE r."Level" = @Level AND NOT r."IsBase")
        """;

    private const string ParentRow = """
        SELECT rv."RevisionId"
             , rv."Version"
        FROM "unshackled-word"."RevisionVerses" rv
            JOIN "unshackled-word"."Revisions" r ON r."Id" = rv."RevisionId"
        WHERE rv."HebRefId" = @HebRefId
            AND r."Level" = @Level
            AND NOT r."IsBase"
        """;

    // A verse without a live row continues from the highest audited version, so a variant that left the seed and comes back does not restart at 1.
    private const string NextVersion = """
        SELECT COALESCE(max(a."Version"), 0) + 1
        FROM "unshackled-word"."RevisionVerseAudit" a
        WHERE a."HebRefId" = @HebRefId
            AND a."RevisionId" = (SELECT r."Id" FROM "unshackled-word"."Revisions" r WHERE r."Level" = @Level AND NOT r."IsBase")
        """;

    private const string InsertVerse = """
        INSERT INTO "unshackled-word"."RevisionVerses"
            ("HebRefId", "RevisionId", "Version", "VerseText", "BaseTextHash", "Note", "CreatedByUserId", "UpdatedByUserId", "BasedOnRevisionId", "BasedOnVersion", "MappingReviewedAt")
        VALUES (@HebRefId, (SELECT r."Id" FROM "unshackled-word"."Revisions" r WHERE r."Level" = @Level AND NOT r."IsBase"), @Version, @VerseText, @BaseHash, @Note, NULL, NULL, @BasedOnRevisionId, @BasedOnVersion, CASE WHEN @Reviewed THEN now() END)
        RETURNING "Id"
        """;

    private const string UpdateVerse = """
        UPDATE "unshackled-word"."RevisionVerses"
        SET "Version"                 = @Version
          , "VerseText"               = @VerseText
          , "BaseTextHash"            = @BaseHash
          , "Note"                    = @Note
          , "BasedOnRevisionId"       = @BasedOnRevisionId
          , "BasedOnVersion"          = @BasedOnVersion
          , "UpdatedByUserId"         = NULL
          , "UpdatedAt"               = now()
          , "MappingReviewedAt"       = CASE WHEN @Reviewed THEN now() END
          , "MappingReviewedByUserId" = NULL
        WHERE "Id" = @Id
        """;

    private const string DeleteWords = """
        DELETE
        FROM "unshackled-word"."RevisionWords"
        WHERE "RevisionVerseId" = @Id
        """;

    private const string DeleteVerse = """
        DELETE
        FROM "unshackled-word"."RevisionVerses"
        WHERE "Id" = @Id
        """;

    private const string InsertWords = """
        INSERT INTO "unshackled-word"."RevisionWords" ("RevisionVerseId", "PositionInVerse", "WordInContext", "PlainWord", "BasePositionInVerse", "Change")
        SELECT @RevisionVerseId, t.pos, t.wic, t.plain, t.basepos, t.change
        FROM unnest(@Positions, @Contexts, @Plains, @BasePositions, @Changes) AS t(pos, wic, plain, basepos, change)
        RETURNING "Id" AS "Id"
                , "PositionInVerse" AS "Position"
        """;

    private const string InsertMapping = """
        INSERT INTO "unshackled-word"."RevisionMapping"
            ("RevisionWordId", "Lang", "StepWordId", "IsAddedWord", "ParentRevisionWordId", "GermanWordPart", "Origin", "CreatedByUserId")
        SELECT t.word, @Lang, t.step, t.added, t.parent, t.part, t.origin, NULL
        FROM unnest(@WordIds, @StepIds, @Added, @ParentIds, @Parts, @Origins) AS t(word, step, added, parent, part, origin)
        """;

    private const string UpsertState = """
        INSERT INTO "unshackled-word"."SeedVerseState" ("Kind", "HebRefId", "ContentHash", "SeedVersion")
        VALUES (@Kind, @HebRefId, @ContentHash, @SeedVersion)
        ON CONFLICT ("Kind", "HebRefId") DO UPDATE
            SET "ContentHash" = excluded."ContentHash"
              , "SeedVersion" = excluded."SeedVersion"
        """;

    private const string DeleteState = """
        DELETE
        FROM "unshackled-word"."SeedVerseState"
        WHERE "Kind" = @Kind
            AND "HebRefId" = @HebRefId
        """;

    private const string InsertSeedImport = """
        INSERT INTO "unshackled-word"."SeedImports"
            ("Kind", "SeedVersion", "ExportedAt", "Rows", "Exact", "Shifted", "Modified", "Orphaned", "VersesUpdated", "VersesKeptLocal")
        VALUES (@Kind, @SeedVersion, @ExportedAt, @Rows, @Exact, @Shifted, @Modified, @Orphaned, @VersesUpdated, @VersesKeptLocal)
        """;

    private readonly IDbConnection _connection;
    private readonly IDbTransaction _transaction;
    private bool _completed;

    public RevisionWriteSession(IDbConnection connection, IDbTransaction transaction)
    {
        _connection = connection;
        _transaction = transaction;
    }

    /// <summary>
    /// Withdraws the variant of (verse, level) (seed update: the variant left the seed): writes the <c>withdraw</c> audit row, then deletes the
    /// live row; words and mapping go with it (cascade). Nothing happens when there is no live row.
    /// </summary>
    public async Task EndActiveAsync(int hebRefId, int level)
    {
        var live = await _connection.QueryFirstOrDefaultAsync<LiveVerse>(LiveRow, new { HebRefId = hebRefId, Level = level }, _transaction);

        if (live is null)
        {
            return;
        }

        await RevisionAuditWriter.WriteAsync(_connection, _transaction, live.Id, RevisionAuditAction.Withdraw, null);
        await _connection.ExecuteAsync(DeleteVerse, new { live.Id }, _transaction);
    }

    /// <summary>
    /// Writes the variant of (verse, level) with its words and mapping: updates the live row in place (version + 1, words replaced, mapping review
    /// taken from the seed) or inserts it (version continues from the audit). Returns the stable <c>RevisionVerses."Id"</c>.
    /// </summary>
    public async Task<int> WriteVariantAsync(RevisionVariantWrite variant)
    {
        var live = await _connection.QueryFirstOrDefaultAsync<LiveVerse>(LiveRow, new { variant.HebRefId, variant.Level }, _transaction);
        var parent = variant.ParentLevel is { } parentLevel
            ? await _connection.QueryFirstOrDefaultAsync<ParentVerse>(ParentRow, new { variant.HebRefId, Level = parentLevel }, _transaction)
            : null;

        var verse = new
        {
            variant.HebRefId,
            variant.Level,
            Version = live is null ? 0 : live.Version + 1,
            variant.VerseText,
            variant.BaseHash,
            variant.Note,
            BasedOnRevisionId = parent?.RevisionId,
            BasedOnVersion = parent?.Version,
            variant.Reviewed,
        };

        int id;

        if (live is null)
        {
            var next = await _connection.ExecuteScalarAsync<int>(NextVersion, new { variant.HebRefId, variant.Level }, _transaction);
            id = await _connection.ExecuteScalarAsync<int>(InsertVerse, new { verse.HebRefId, verse.Level, Version = next, verse.VerseText, verse.BaseHash, verse.Note, verse.BasedOnRevisionId, verse.BasedOnVersion, verse.Reviewed }, _transaction);
        }
        else
        {
            id = live.Id;
            await _connection.ExecuteAsync(UpdateVerse, new { Id = id, verse.Version, verse.VerseText, verse.BaseHash, verse.Note, verse.BasedOnRevisionId, verse.BasedOnVersion, verse.Reviewed }, _transaction);
            await _connection.ExecuteAsync(DeleteWords, new { Id = id }, _transaction);
        }

        var words = variant.Words.OrderBy(w => w.Position).ToList();
        var inserted = (await _connection.QueryAsync<InsertedWord>(
            InsertWords,
            new
            {
                RevisionVerseId = id,
                Positions = words.Select(w => w.Position).ToArray(),
                Contexts = words.Select(w => w.WordInContext).ToArray(),
                Plains = words.Select(w => w.PlainWord).ToArray(),
                BasePositions = words.Select(w => w.BasePosition).ToArray(),
                Changes = words.Select(w => w.Change).ToArray()
            },
            _transaction,
            CommandTimeoutSeconds)).ToDictionary(w => w.Position, w => w.Id);

        if (variant.Mapping.Count > 0)
        {
            await _connection.ExecuteAsync(
                InsertMapping,
                new
                {
                    variant.Lang,
                    WordIds = variant.Mapping.Select(m => inserted[m.Position]).ToArray(),
                    StepIds = variant.Mapping.Select(m => m.StepWordId).ToArray(),
                    Added = variant.Mapping.Select(m => m.Added).ToArray(),
                    ParentIds = variant.Mapping.Select(m => m.ParentPosition is { } p ? inserted[p] : (int?)null).ToArray(),
                    Parts = variant.Mapping.Select(m => m.Part).ToArray(),
                    Origins = variant.Mapping.Select(m => m.Origin).ToArray()
                },
                _transaction,
                CommandTimeoutSeconds);
        }

        await RevisionAuditWriter.WriteAsync(_connection, _transaction, id, RevisionAuditAction.Text, null);

        return id;
    }

    public async Task UpsertStateAsync(string seedVersion, int hebRefId, int level, string contentHash)
    {
        await _connection.ExecuteAsync(
            UpsertState,
            new { Kind = RevisionSeedRepository.StateKindPrefix + level, HebRefId = hebRefId, ContentHash = contentHash, SeedVersion = seedVersion },
            _transaction);
    }

    public async Task DeleteStateAsync(int hebRefId, int level)
    {
        await _connection.ExecuteAsync(DeleteState, new { Kind = RevisionSeedRepository.StateKindPrefix + level, HebRefId = hebRefId }, _transaction);
    }

    public async Task CompleteAsync(RevisionImportRecord record)
    {
        await _connection.ExecuteAsync(
            InsertSeedImport,
            new
            {
                Kind = RevisionSeedRepository.Kind,
                record.SeedVersion,
                record.ExportedAt,
                record.Rows,
                record.Exact,
                record.Shifted,
                record.Modified,
                record.Orphaned,
                record.VersesUpdated,
                record.VersesKeptLocal
            },
            _transaction);

        _transaction.Commit();
        _completed = true;
    }

    public void Dispose()
    {
        if (!_completed)
        {
            _transaction.Rollback();
        }

        _transaction.Dispose();
        _connection.Dispose();
    }

    private sealed class LiveVerse
    {
        public int Id { get; init; }
        public int Version { get; init; }
    }

    private sealed class ParentVerse
    {
        public int RevisionId { get; init; }
        public int Version { get; init; }
    }

    private sealed class InsertedWord
    {
        public int Id { get; init; }
        public int Position { get; init; }
    }
}
