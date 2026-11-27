using System.Data;
using Dapper;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.Models.Vocab;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Domain.WebApi.Vocab;

namespace UnshackledWord.Infrastructure.Vocab;

/// <summary>A root to insert or update; identity is (<see cref="Track"/>, normalized <see cref="Root"/>, <see cref="Homograph"/>).</summary>
public sealed class VocabRootUpsert
{
    public string Track { get; init; } = string.Empty;
    public string Root { get; init; } = string.Empty;
    public short Homograph { get; init; } = 1;

    /// <summary>Required for Greek (the check sees the inserted row, also on update); for Hebrew null keeps a key the row already has.</summary>
    public string? StrongsKey { get; init; }

    /// <summary>Null keeps the parent the row already has.</summary>
    public int? ParentRootId { get; init; }

    public int? UserId { get; init; }
}

/// <summary>The root of one word key (a <c>VocabStrongsRoots</c> row).</summary>
public sealed class VocabWordRootUpsert
{
    public string Lang { get; init; } = string.Empty;
    public string StrongsKey { get; init; } = string.Empty;
    public int VocabRootId { get; init; }

    /// <summary>The stripped prepositions in word order (<c>σύν + ἐπί</c>); null for a simple word and for Hebrew.</summary>
    public string? Prefixes { get; init; }

    /// <summary>Strong's keys of the prepositions, same order as <see cref="Prefixes"/>.</summary>
    public string[]? PrefixKeys { get; init; }

    /// <summary><c>prefill</c>, <c>agent</c> or <c>manual</c>.</summary>
    public string Origin { get; init; } = string.Empty;

    public int? AgentRunId { get; init; }
    public int? UserId { get; init; }
}

/// <summary>A <c>VocabRoots</c> row.</summary>
public sealed class VocabRootRow
{
    public int Id { get; init; }
    public string Track { get; init; } = string.Empty;
    public string Root { get; init; } = string.Empty;
    public string RootNormalized { get; init; } = string.Empty;
    public short Homograph { get; init; }
    public string? StrongsKey { get; init; }
    public int? ParentRootId { get; init; }
}

/// <summary>
/// Roots of vocab entries (roadmap 0030.1): reads go through the <c>VocabEntryRoots</c> view (root of the entry's main key),
/// writes upsert <c>VocabRoots</c> and <c>VocabStrongsRoots</c>. A <c>manual</c> word row is never overwritten by a non-manual one.
/// Roots do not bump <c>VocabEntries."ContentVersion"</c>.
/// </summary>
public sealed class VocabRootRepository
{
    private const string SelectByEntryIds = """
        SELECT er."VocabEntryId"
             , er."VocabRootId"     AS "Id"
             , er."Root"
             , er."RootStrongsKey"  AS "StrongsKey"
             , er."Prefixes"
             , er."Origin"
             , er."IsSimple"
        FROM "unshackled-word"."VocabEntryRoots" er
        WHERE er."VocabEntryId" = ANY(@Ids)
        """;

    private const string SelectRoot = """
        SELECT r."Id"
             , r."Track"::text  AS "Track"
             , r."Root"
             , r."RootNormalized"
             , r."Homograph"
             , r."StrongsKey"
             , r."ParentRootId"
        FROM "unshackled-word"."VocabRoots" r
        WHERE r."Id" = @Id
        """;

    private const string SelectCounts = """
        WITH active AS
        (
            SELECT e."Id"
                 , (e."IsMorpheme" OR e."IsProperName") AS "NotApplicable"
            FROM "unshackled-word"."VocabEntries" e
            WHERE 1=1
                AND e."RetiredAt" IS NULL
        )
        SELECT count(*) FILTER (WHERE NOT a."NotApplicable" AND er."Origin" = 'prefill')::int AS "Prefill"
             , count(*) FILTER (WHERE NOT a."NotApplicable" AND er."Origin" = 'agent')::int   AS "Agent"
             , count(*) FILTER (WHERE NOT a."NotApplicable" AND er."Origin" = 'manual')::int  AS "Manual"
             , count(*) FILTER (WHERE NOT a."NotApplicable" AND er."VocabEntryId" IS NULL)::int AS "Missing"
             , count(*) FILTER (WHERE a."NotApplicable")::int                                 AS "NotApplicable"
        FROM active a
            LEFT JOIN "unshackled-word"."VocabEntryRoots" er ON er."VocabEntryId" = a."Id"
        """;

    private const string UpsertRoot = """
        INSERT INTO "unshackled-word"."VocabRoots" AS r
            ("Track", "Root", "RootNormalized", "Homograph", "StrongsKey", "ParentRootId", "UpdatedAt", "UpdatedByUserId")
        VALUES (@Track, @Root, @RootNormalized, @Homograph, @StrongsKey, @ParentRootId, now(), @UserId)
        ON CONFLICT ("Track", "RootNormalized", "Homograph") DO UPDATE
            SET "Root"            = EXCLUDED."Root"
              , "StrongsKey"      = COALESCE(EXCLUDED."StrongsKey", r."StrongsKey")
              , "ParentRootId"    = COALESCE(EXCLUDED."ParentRootId", r."ParentRootId")
              , "UpdatedAt"       = now()
              , "UpdatedByUserId" = EXCLUDED."UpdatedByUserId"
        RETURNING r."Id"
        """;

    // a manual row is only replaced by another manual write
    private const string UpsertWordRoot = """
        INSERT INTO "unshackled-word"."VocabStrongsRoots" AS s
            ("Lang", "StrongsKey", "VocabRootId", "Prefixes", "PrefixKeys", "Origin", "AgentRunId", "UpdatedAt", "UpdatedByUserId")
        VALUES (@Lang, @StrongsKey, @VocabRootId, @Prefixes, @PrefixKeys, @Origin, @AgentRunId, now(), @UserId)
        ON CONFLICT ("Lang", "StrongsKey") DO UPDATE
            SET "VocabRootId"     = EXCLUDED."VocabRootId"
              , "Prefixes"        = EXCLUDED."Prefixes"
              , "PrefixKeys"      = EXCLUDED."PrefixKeys"
              , "Origin"          = EXCLUDED."Origin"
              , "AgentRunId"      = EXCLUDED."AgentRunId"
              , "UpdatedAt"       = now()
              , "UpdatedByUserId" = EXCLUDED."UpdatedByUserId"
            WHERE s."Origin" <> 'manual' OR EXCLUDED."Origin" = 'manual'
        """;

    /// <summary>
    /// Family filter for a query over <c>VocabEntries e</c> (roadmap 0030.3), parameters <c>@RootId</c> (int, null = off) and <c>@SubRoots</c> (bool):
    /// the entries whose main key points at the root (plus the root's own entry when it has no row), with sub-roots (through <c>ParentRootId</c>, at most 4 levels) when asked.
    /// </summary>
    public const string FamilyCondition = """
        (@RootId::int IS NULL OR e."Id" IN
            (WITH RECURSIVE fam ("Id", "Depth") AS
             (
                 SELECT r."Id", 0
                 FROM "unshackled-word"."VocabRoots" r
                 WHERE r."Id" = @RootId::int
                 UNION ALL
                 SELECT c."Id", f."Depth" + 1
                 FROM "unshackled-word"."VocabRoots" c
                     JOIN fam f ON c."ParentRootId" = f."Id"
                 WHERE @SubRoots AND f."Depth" < 4
             )
             SELECT er."VocabEntryId"
             FROM "unshackled-word"."VocabEntryRoots" er
             WHERE er."VocabRootId" IN (SELECT fam."Id" FROM fam)
             UNION
             -- a root's own entry belongs to its family even without a (simple-word) row of its own
             SELECT vs."VocabEntryId"
             FROM "unshackled-word"."VocabStrongs" vs
                 JOIN "unshackled-word"."VocabRoots" fr ON fr."StrongsKey" = vs."StrongsKey"
             WHERE fr."Id" IN (SELECT fam."Id" FROM fam)
                 AND NOT EXISTS (SELECT 1 FROM "unshackled-word"."VocabEntryRoots" x WHERE x."VocabEntryId" = vs."VocabEntryId")))
        """;

    /// <summary>ORDER BY term: with a root filter the root's own entry (it carries the root's Strong's key) comes first.</summary>
    public const string OwnEntryFirst = """
        (@RootId::int IS NOT NULL AND EXISTS (SELECT 1
                                              FROM "unshackled-word"."VocabStrongs" ovs
                                                  JOIN "unshackled-word"."VocabRoots" orr ON orr."StrongsKey" = ovs."StrongsKey"
                                              WHERE ovs."VocabEntryId" = e."Id" AND orr."Id" = @RootId::int)) DESC
        """;

    /// <summary>The editor filter "no root": active entries a root applies to whose main key has no row (same rule as the counts).</summary>
    public const string MissingRootCondition = """
        (NOT @MissingRoot OR (e."RetiredAt" IS NULL AND NOT e."IsMorpheme" AND NOT e."IsProperName"
                              AND NOT EXISTS (SELECT 1 FROM "unshackled-word"."VocabEntryRoots" mr WHERE mr."VocabEntryId" = e."Id")))
        """;

    // the family of the root: ranked, active entries, the root's own entry first
    private const string SelectMembers = """
        SELECT e."Id" AS "EntryId"
             , e."Lemma"
             , e."FrequencyRank" AS "Rank"
             , (SELECT COALESCE(m."GermanOverride", m."German")
                FROM "unshackled-word"."VocabMeanings" m
                WHERE m."VocabEntryId" = e."Id" AND m."IsAccepted"
                ORDER BY m."Rank", m."Id"
                LIMIT 1) AS "Meaning"
        FROM "unshackled-word"."VocabEntries" e
        WHERE 1=1
            AND e."RetiredAt" IS NULL
            AND e."FrequencyRank" IS NOT NULL
            AND
        """ + "\n" + FamilyCondition + """

        ORDER BY
        """ + "\n" + OwnEntryFirst + """
        , e."FrequencyRank", e."Id"
        LIMIT @Take
        """;

    private const string SelectMemberTotal = """
        SELECT count(*)::int
        FROM "unshackled-word"."VocabEntries" e
        WHERE 1=1
            AND e."RetiredAt" IS NULL
            AND e."FrequencyRank" IS NOT NULL
            AND
        """ + "\n" + FamilyCondition;

    private const string SelectParent = """
        SELECT p."Id", p."Root"
        FROM "unshackled-word"."VocabRoots" r
            JOIN "unshackled-word"."VocabRoots" p ON p."Id" = r."ParentRootId"
        WHERE r."Id" = @Id
        """;

    private const string SelectSubRoots = """
        SELECT c."Id", c."Root"
        FROM "unshackled-word"."VocabRoots" c
        WHERE c."ParentRootId" = @Id
        ORDER BY c."RootNormalized", c."Homograph"
        """;

    // the editor's view of the root of an entry's main key (roadmap 0030.5)
    private const string SelectEditorRoot = """
        SELECT er."VocabRootId" AS "RootId"
             , er."Root"
             , er."RootStrongsKey" AS "StrongsKey"
             , r."Homograph"
             , er."PrefixKeys"
             , er."Prefixes"
             , er."Origin"
             , er."IsSimple"
        FROM "unshackled-word"."VocabEntryRoots" er
            JOIN "unshackled-word"."VocabRoots" r ON r."Id" = er."VocabRootId"
        WHERE er."VocabEntryId" = @Id
        """;

    private const string SelectAncestors = """
        WITH RECURSIVE up ("Id", "Root", "ParentRootId", "Depth") AS
        (
            SELECT p."Id", p."Root", p."ParentRootId", 1
            FROM "unshackled-word"."VocabRoots" r
                JOIN "unshackled-word"."VocabRoots" p ON p."Id" = r."ParentRootId"
            WHERE r."Id" = @Id
            UNION ALL
            SELECT p."Id", p."Root", p."ParentRootId", u."Depth" + 1
            FROM "unshackled-word"."VocabRoots" p
                JOIN up u ON p."Id" = u."ParentRootId"
            WHERE u."Depth" < 4
        )
        SELECT up."Id", up."Root"
        FROM up
        ORDER BY up."Depth"
        """;

    // Greek: active entries by lemma prefix, their root row (by the main key) when there is one
    private const string SearchGreek = """
        SELECT COALESCE(r."Root", e."Lemma") AS "Root"
             , mk."StrongsKey"
             , r."Id" AS "RootId"
             , COALESCE(r."Homograph", 1::int2) AS "Homograph"
             , (SELECT COALESCE(m."GermanOverride", m."German")
                FROM "unshackled-word"."VocabMeanings" m
                WHERE m."VocabEntryId" = e."Id" AND m."IsAccepted"
                ORDER BY m."Rank", m."Id"
                LIMIT 1) AS "Meaning"
        FROM "unshackled-word"."VocabEntries" e
            CROSS JOIN LATERAL
            (
                SELECT vs."StrongsKey"
                FROM "unshackled-word"."VocabStrongs" vs
                WHERE vs."VocabEntryId" = e."Id"
                ORDER BY vs."Occurrences" DESC, vs."StrongsKey"
                LIMIT 1
            ) mk
            LEFT JOIN "unshackled-word"."VocabRoots" r ON r."StrongsKey" = mk."StrongsKey"
        WHERE 1=1
            AND e."Track" = 'G'
            AND e."RetiredAt" IS NULL
            AND NOT e."IsMorpheme"
            AND NOT e."IsProperName"
            AND e."LemmaNormalized" LIKE @Like
        ORDER BY e."FrequencyRank" NULLS LAST, e."Id"
        LIMIT @Take
        """;

    private const string SearchHebrew = """
        SELECT r."Root"
             , r."StrongsKey"
             , r."Id" AS "RootId"
             , r."Homograph"
             , (SELECT COALESCE(m."GermanOverride", m."German")
                FROM "unshackled-word"."VocabStrongs" vs
                    JOIN "unshackled-word"."VocabMeanings" m ON m."VocabEntryId" = vs."VocabEntryId" AND m."IsAccepted"
                WHERE vs."StrongsKey" = r."StrongsKey"
                ORDER BY m."Rank", m."Id"
                LIMIT 1) AS "Meaning"
        FROM "unshackled-word"."VocabRoots" r
        WHERE r."Track" = 'H' AND r."RootNormalized" LIKE @Like
        ORDER BY r."RootNormalized", r."Homograph"
        LIMIT @Take
        """;

    private readonly IDbReader _dbReader;
    private readonly IDbConnectionFactory _connectionFactory;

    public VocabRootRepository(IDbReader dbReader, IDbConnectionFactory connectionFactory)
    {
        _dbReader = dbReader;
        _connectionFactory = connectionFactory;
    }

    /// <summary>The root per entry id; entries without a known root are missing from the result.</summary>
    public Task<IReadOnlyDictionary<int, VocabRootResponse>> GetByEntryIdsAsync(IReadOnlyCollection<int> entryIds) =>
        ReadByEntryIdsAsync(_dbReader, entryIds);

    /// <summary>Same as <see cref="GetByEntryIdsAsync"/> on a given reader, for the list repositories that read through their own <see cref="IDbReader"/>.</summary>
    public static async Task<IReadOnlyDictionary<int, VocabRootResponse>> ReadByEntryIdsAsync(IDbReader dbReader, IReadOnlyCollection<int> entryIds)
    {
        if (entryIds.Count == 0)
        {
            return new Dictionary<int, VocabRootResponse>();
        }

        var rows = await dbReader.ReadAsListAsync<EntryRootRow>(SelectByEntryIds, new { Ids = entryIds.ToArray() });

        return ToResponses(rows);
    }

    /// <summary>Same as <see cref="GetByEntryIdsAsync"/> inside the caller's connection and transaction (the study session).</summary>
    public static async Task<IReadOnlyDictionary<int, VocabRootResponse>> ReadByEntryIdsAsync(IDbConnection connection, IDbTransaction? transaction, IReadOnlyCollection<int> entryIds)
    {
        if (entryIds.Count == 0)
        {
            return new Dictionary<int, VocabRootResponse>();
        }

        var rows = await connection.QueryAsync<EntryRootRow>(new CommandDefinition(SelectByEntryIds, new { Ids = entryIds.ToArray() }, transaction));

        return ToResponses(rows);
    }

    private static Dictionary<int, VocabRootResponse> ToResponses(IEnumerable<EntryRootRow> rows)
    {
        return rows.ToDictionary(r => r.VocabEntryId, r => new VocabRootResponse
        {
            Id = r.Id,
            Root = r.Root,
            StrongsKey = r.StrongsKey,
            Prefixes = r.Prefixes,
            Origin = r.Origin,
            IsSimple = r.IsSimple
        });
    }

    /// <summary>The root of the entry's main key for the editor (prefix keys, homograph, parents nearest first); null while there is none.</summary>
    public static async Task<VocabEditorRootResponse?> ReadEditorRootAsync(IDbReader dbReader, int entryId)
    {
        var root = await dbReader.ReadFirstOrDefaultAsync<VocabEditorRootResponse>(SelectEditorRoot, new { Id = entryId });

        if (root is null)
        {
            return null;
        }

        root.Ancestors = (await dbReader.ReadAsListAsync<VocabRootRefResponse>(SelectAncestors, new { Id = root.RootId })).ToList();

        return root;
    }

    /// <summary>Root candidates of one track by typed text (lemma prefix for Greek, consonants for Hebrew); empty text lists nothing.</summary>
    public async Task<List<VocabRootCandidateResponse>> SearchAsync(char track, string? text, int take)
    {
        var normalized = VocabText.Normalize(text);

        if (normalized.Length == 0)
        {
            return [];
        }

        var parameters = new { Like = normalized.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%", Take = Math.Clamp(take, 1, 25) };

        return (await _dbReader.ReadAsListAsync<VocabRootCandidateResponse>(track == 'G' ? SearchGreek : SearchHebrew, parameters)).ToList();
    }

    /// <summary>Active entries by root origin, those without a root and those a root does not apply to (morphemes, proper names).</summary>
    public async Task<VocabRootCountsResponse> GetCountsAsync() =>
        await _dbReader.ReadFirstOrDefaultAsync<VocabRootCountsResponse>(SelectCounts) ?? new VocabRootCountsResponse();

    public Task<VocabRootRow?> GetRootAsync(int id) =>
        _dbReader.ReadFirstOrDefaultAsync<VocabRootRow>(SelectRoot, new { Id = id });

    /// <summary>
    /// The family card of one root (roadmap 0030.3): the root, its parent, its direct sub-roots and at most <paramref name="take"/> members
    /// (the whole family including sub-roots, the root's own entry first, then by rank) plus the member total. Null for an unknown id.
    /// </summary>
    public async Task<VocabRootDetailResponse?> GetDetailAsync(int id, int take = VocabRootDetailResponse.MaxMembers)
    {
        var root = await GetRootAsync(id);

        if (root is null)
        {
            return null;
        }

        var parameters = new { RootId = (int?)id, SubRoots = true, Take = take };

        return new VocabRootDetailResponse
        {
            Id = root.Id,
            Root = root.Root,
            StrongsKey = root.StrongsKey,
            Parent = await _dbReader.ReadFirstOrDefaultAsync<VocabRootRefResponse>(SelectParent, new { Id = id }),
            SubRoots = (await _dbReader.ReadAsListAsync<VocabRootRefResponse>(SelectSubRoots, new { Id = id })).ToList(),
            Members = (await _dbReader.ReadAsListAsync<VocabRootMemberResponse>(SelectMembers, parameters)).ToList(),
            MemberTotal = await _dbReader.ExecuteScalarAsync<int>(SelectMemberTotal, parameters)
        };
    }

    /// <summary>Inserts the root or updates the one with the same identity; returns its id.</summary>
    public async Task<int> UpsertRootAsync(VocabRootUpsert root, CancellationToken token = default)
    {
        using var connection = await _connectionFactory.CreateDbConnectionAsync(token);

        return await UpsertRootAsync(connection, null, root, token);
    }

    public static Task<int> UpsertRootAsync(IDbConnection connection, IDbTransaction? transaction, VocabRootUpsert root, CancellationToken token = default)
    {
        var parameters = new
        {
            root.Track,
            root.Root,
            RootNormalized = VocabText.Normalize(root.Root),
            root.Homograph,
            root.StrongsKey,
            root.ParentRootId,
            root.UserId
        };

        return connection.ExecuteScalarAsync<int>(new CommandDefinition(UpsertRoot, parameters, transaction, cancellationToken: token));
    }

    /// <summary>Writes the root of one word key; false when a manual row was kept because the write is not manual.</summary>
    public async Task<bool> UpsertWordRootAsync(VocabWordRootUpsert row, CancellationToken token = default)
    {
        using var connection = await _connectionFactory.CreateDbConnectionAsync(token);

        return await UpsertWordRootAsync(connection, null, row, token);
    }

    public static async Task<bool> UpsertWordRootAsync(IDbConnection connection, IDbTransaction? transaction, VocabWordRootUpsert row, CancellationToken token = default)
    {
        var parameters = new
        {
            row.Lang,
            row.StrongsKey,
            row.VocabRootId,
            row.Prefixes,
            PrefixKeys = row.PrefixKeys is { Length: > 0 } ? row.PrefixKeys : null,
            row.Origin,
            row.AgentRunId,
            row.UserId
        };

        return await connection.ExecuteAsync(new CommandDefinition(UpsertWordRoot, parameters, transaction, cancellationToken: token)) > 0;
    }

    private sealed class EntryRootRow
    {
        public int VocabEntryId { get; init; }
        public int Id { get; init; }
        public string Root { get; init; } = string.Empty;
        public string? StrongsKey { get; init; }
        public string? Prefixes { get; init; }
        public string Origin { get; init; } = string.Empty;
        public bool IsSimple { get; init; }
    }
}
