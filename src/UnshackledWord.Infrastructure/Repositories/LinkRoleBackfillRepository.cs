using System.Data;
using Dapper;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Features.LinkRoles.Backfill;

namespace UnshackledWord.Infrastructure.Repositories;

/// <summary>Dapper side of the link-roles backfill (roadmap 0018.4). Apply and undo are one transaction each.</summary>
public sealed class LinkRoleBackfillRepository : ILinkRoleBackfillRepository
{
    /// <summary>Rows per INSERT of change rows.</summary>
    public const int BatchSize = 10_000;

    private const int CommandTimeoutSeconds = 1800;

    private readonly IDbReader _dbReader;
    private readonly IDbConnectionFactory _factory;

    public LinkRoleBackfillRepository(IDbReader dbReader, IDbConnectionFactory factory)
    {
        _dbReader = dbReader;
        _factory = factory;
    }

    public async Task<IReadOnlyList<int>> GetBookIdsAsync(CancellationToken ct) =>
        (await _dbReader.ReadAsListAsync<int>(LinkRoleBackfillSql.BookIds)).ToList();

    public async Task<LinkRoleBookData> LoadBookAsync(int bookId, CancellationToken ct)
    {
        var param = new { BookId = bookId };
        var name = await _dbReader.ReadFirstOrDefaultAsync<string>(LinkRoleBackfillSql.BookName, param);
        var words = await _dbReader.ReadAsListAsync<LinkRoleWordRow>(LinkRoleBackfillSql.Words, param);
        var greek = await _dbReader.ReadAsListAsync<LinkRoleMappingRow>(LinkRoleBackfillSql.MappingRows(MappingLang.Greek), param);
        var hebrew = await _dbReader.ReadAsListAsync<LinkRoleMappingRow>(LinkRoleBackfillSql.MappingRows(MappingLang.Hebrew), param);
        var greekSources = await _dbReader.ReadAsListAsync<LinkRoleSourceRow>(LinkRoleBackfillSql.GreekSources, param);
        var hebrewSources = await _dbReader.ReadAsListAsync<LinkRoleSourceRow>(LinkRoleBackfillSql.HebrewSources, param);

        return new LinkRoleBookData(bookId, name ?? bookId.ToString(), words.ToList(), greek.ToList(), hebrew.ToList(), greekSources.ToList(), hebrewSources.ToList());
    }

    public async Task<IReadOnlySet<string>> LoadKnownLemmasAsync(CancellationToken ct) =>
        (await _dbReader.ReadAsListAsync<string>(LinkRoleBackfillSql.KnownLemmas)).ToHashSet(StringComparer.OrdinalIgnoreCase);

    public async Task<int> CountActiveAgentRunsAsync(CancellationToken ct) =>
        await _dbReader.ExecuteScalarAsync<int>(LinkRoleBackfillSql.ActiveAgentRuns);

    public Task<LinkRoleChangeSetInfo?> GetAppliedAsync(CancellationToken ct) =>
        _dbReader.ReadFirstOrDefaultAsync<LinkRoleChangeSetInfo>(LinkRoleBackfillSql.Applied, new { Kind = LinkRoleChangeSetKinds.Apply });

    public async Task<DateTime?> GetLinkRolesAppliedAtAsync(CancellationToken ct) =>
        await _dbReader.ExecuteScalarAsync<bool>(LinkRoleBackfillSql.AppliedAtColumnExists)
            ? await _dbReader.ExecuteScalarAsync<DateTime?>(LinkRoleBackfillSql.ReadAppliedAt)
            : null;

    public async Task<LinkRoleApplyResult> ApplyAsync(IReadOnlyList<MappingRowChange> mappings, IReadOnlyList<LemmaRowChange> lemmas, int userId, string note, CancellationToken ct)
    {
        using var connection = await _factory.CreateDbConnectionAsync(ct);
        using var transaction = connection.BeginTransaction();

        var hasAppliedAt = await connection.ExecuteScalarAsync<bool>(Command(LinkRoleBackfillSql.AppliedAtColumnExists, null, transaction, ct));
        var previous = hasAppliedAt ? await connection.ExecuteScalarAsync<DateTime?>(Command(LinkRoleBackfillSql.ReadAppliedAt, null, transaction, ct)) : null;

        var changeSetId = await connection.ExecuteScalarAsync<int>(Command(LinkRoleBackfillSql.InsertChangeSet, new
        {
            UserId = userId,
            Kind = LinkRoleChangeSetKinds.Apply,
            Note = note,
            WordCount = lemmas.Count,
            RevertOf = (int?)null,
            MappingRows = mappings.Count,
            Previous = previous,
        }, transaction, ct));

        foreach (var batch in mappings.Chunk(BatchSize))
        {
            await connection.ExecuteAsync(Command(LinkRoleBackfillSql.InsertMappingRows, MappingRowsParam(changeSetId, batch), transaction, ct));
        }

        foreach (var lang in new[] { MappingLang.Greek, MappingLang.Hebrew })
        {
            var expected = mappings.Count(m => m.Lang == lang);
            var updated = await connection.ExecuteAsync(Command(LinkRoleBackfillSql.ApplyMapping(lang), new { ChangeSetId = changeSetId, Lang = lang.ToString() }, transaction, ct));
            if (updated != expected)
            {
                throw new InvalidOperationException($"{expected - updated} {(lang == MappingLang.Greek ? "Greek" : "Hebrew")} mapping rows changed since the change set was computed; nothing was written. Run again.");
            }
        }

        if (lemmas.Count > 0)
        {
            var written = await connection.ExecuteScalarAsync<int>(Command(LinkRoleBackfillSql.ApplyLemmas, new
            {
                ChangeSetId = changeSetId,
                UserId = userId,
                ElbWordIds = lemmas.Select(l => l.ElbWordId).ToArray(),
                HebRefIds = lemmas.Select(l => l.HebRefId).ToArray(),
                Positions = lemmas.Select(l => l.PositionInVerse).ToArray(),
                OldLemmas = lemmas.Select(l => l.OldLemma).ToArray(),
                NewLemmas = lemmas.Select(l => l.NewLemma).ToArray(),
            }, transaction, ct));
            if (written != lemmas.Count)
            {
                throw new InvalidOperationException($"{lemmas.Count - written} lemmas changed since the change set was computed; nothing was written. Run again.");
            }

            await connection.ExecuteAsync(Command(LinkRoleBackfillSql.RefreshLemmaText, new { ChangeSetId = changeSetId }, transaction, ct));
        }

        if (hasAppliedAt)
        {
            await connection.ExecuteAsync(Command(LinkRoleBackfillSql.SetAppliedAtNow, null, transaction, ct));
        }

        transaction.Commit();
        return new LinkRoleApplyResult(changeSetId, mappings.Count, lemmas.Count);
    }

    public async Task<LinkRoleUndoResult> UndoAsync(int changeSetId, int userId, CancellationToken ct)
    {
        using var connection = await _factory.CreateDbConnectionAsync(ct);
        using var transaction = connection.BeginTransaction();

        var set = await connection.QueryFirstOrDefaultAsync<ChangeSetRow>(Command(LinkRoleBackfillSql.LockChangeSet, new { ChangeSetId = changeSetId }, transaction, ct))
                  ?? throw new InvalidOperationException($"Change set {changeSetId} not found.");
        if (set.Kind != LinkRoleChangeSetKinds.Apply)
        {
            throw new InvalidOperationException($"Change set {changeSetId} is a '{set.Kind}' change set, not a link-roles backfill.");
        }

        if (set.RevertedAt is not null)
        {
            throw new InvalidOperationException($"Change set {changeSetId} was already undone at {set.RevertedAt:u}.");
        }

        var undoId = await connection.ExecuteScalarAsync<int>(Command(LinkRoleBackfillSql.InsertChangeSet, new
        {
            UserId = userId,
            Kind = LinkRoleChangeSetKinds.Undo,
            Note = $"undo of link-roles change set {changeSetId}",
            WordCount = 0,
            RevertOf = (int?)changeSetId,
            MappingRows = 0,
            Previous = (DateTime?)null,
        }, transaction, ct));

        var mappingTotal = await connection.ExecuteScalarAsync<int>(Command(LinkRoleBackfillSql.CountMappingRows, new { ChangeSetId = changeSetId }, transaction, ct));
        var mappingRestored = 0;
        foreach (var lang in new[] { MappingLang.Greek, MappingLang.Hebrew })
        {
            mappingRestored += await connection.ExecuteAsync(Command(LinkRoleBackfillSql.UndoMapping(lang), new { ChangeSetId = changeSetId, Lang = lang.ToString() }, transaction, ct));
        }

        var lemmaTotal = await connection.ExecuteScalarAsync<int>(Command(LinkRoleBackfillSql.CountLemmaRows, new { ChangeSetId = changeSetId }, transaction, ct));
        var lemmasRestored = 0;
        if (lemmaTotal > 0)
        {
            lemmasRestored = await connection.ExecuteScalarAsync<int>(Command(LinkRoleBackfillSql.UndoLemmas, new { ChangeSetId = changeSetId, UndoChangeSetId = undoId, UserId = userId }, transaction, ct));
            await connection.ExecuteAsync(Command(LinkRoleBackfillSql.RefreshLemmaText, new { ChangeSetId = changeSetId }, transaction, ct));
        }

        if (await connection.ExecuteScalarAsync<bool>(Command(LinkRoleBackfillSql.AppliedAtColumnExists, null, transaction, ct)))
        {
            await connection.ExecuteAsync(Command(LinkRoleBackfillSql.RestoreAppliedAt, new { ChangeSetId = changeSetId }, transaction, ct));
        }

        await connection.ExecuteAsync(Command(LinkRoleBackfillSql.FinishUndo, new { ChangeSetId = changeSetId, UndoChangeSetId = undoId, Restored = lemmasRestored }, transaction, ct));

        transaction.Commit();
        return new LinkRoleUndoResult(changeSetId, undoId, mappingRestored, mappingTotal - mappingRestored, lemmasRestored, lemmaTotal - lemmasRestored);
    }

    /// <summary>Column arrays of one batch for <see cref="LinkRoleBackfillSql.InsertMappingRows"/>.</summary>
    public static object MappingRowsParam(int changeSetId, IReadOnlyCollection<MappingRowChange> batch) => new
    {
        ChangeSetId = changeSetId,
        Lang = batch.Select(m => m.Lang.ToString()).ToArray(),
        MappingId = batch.Select(m => m.MappingId).ToArray(),
        HebRefId = batch.Select(m => m.HebRefId).ToArray(),
        ElbWordId = batch.Select(m => m.ElbWordId).ToArray(),
        Rule = batch.Select(m => m.Rule).ToArray(),
        BeforeRole = batch.Select(m => m.Before.Role).ToArray(),
        BeforeStrongsNumber = batch.Select(m => m.Before.StrongsNumber).ToArray(),
        BeforeIsAddedWord = batch.Select(m => m.Before.IsAddedWord).ToArray(),
        BeforeParentGermanWordId = batch.Select(m => m.Before.ParentGermanWordId).ToArray(),
        BeforeStepWordId = batch.Select(m => m.Before.StepWordId).ToArray(),
        AfterRole = batch.Select(m => m.After.Role).ToArray(),
        AfterStrongsNumber = batch.Select(m => m.After.StrongsNumber).ToArray(),
        AfterIsAddedWord = batch.Select(m => m.After.IsAddedWord).ToArray(),
        AfterParentGermanWordId = batch.Select(m => m.After.ParentGermanWordId).ToArray(),
        AfterStepWordId = batch.Select(m => m.After.StepWordId).ToArray(),
    };

    private static CommandDefinition Command(string sql, object? param, IDbTransaction transaction, CancellationToken ct) =>
        new(sql, param, transaction, CommandTimeoutSeconds, cancellationToken: ct);

    private sealed class ChangeSetRow
    {
        public string Kind { get; set; } = string.Empty;
        public DateTime? RevertedAt { get; set; }
    }
}

/// <summary>SQL of <see cref="LinkRoleBackfillRepository"/>, public so tests can assert its shape.</summary>
public static class LinkRoleBackfillSql
{
    public const string BookIds = """
                                  SELECT DISTINCT ew."BibleBookId"
                                  FROM "unshackled-word"."Elb1871Words" ew
                                  ORDER BY ew."BibleBookId"
                                  """;

    public const string BookName = """
                                   SELECT bb."Name"
                                   FROM "unshackled-word"."BibleBooks" bb
                                   WHERE 1=1
                                       AND bb."Id" = @BookId
                                   """;

    /// <summary>German words with morphology; <c>Text</c> is the plain word (no punctuation: rule 6 writes it as lemma).</summary>
    public const string Words = """
                                SELECT ew."Id"
                                     , ew."HebRefId"
                                     , ew."PositionInVerse"
                                     , COALESCE(NULLIF(ew."PlainWord", ''), ew."WordInContext") "Text"
                                     , em."Lemma"
                                     , em."Stts"
                                     , em."Person"
                                     , em."Number"
                                     , em."Gender"
                                     , em."Case"
                                     , ew."Chapter"
                                     , ew."Verse"
                                FROM "unshackled-word"."Elb1871Words" ew
                                    LEFT JOIN "unshackled-word"."Elb1871Morphology" em ON em."HebRefId" = ew."HebRefId" AND em."PositionInVerse" = ew."PositionInVerse"
                                WHERE 1=1
                                    AND ew."BibleBookId" = @BookId
                                ORDER BY ew."HebRefId"
                                       , ew."PositionInVerse"
                                       , ew."Id"
                                """;

    public static string MappingRows(char lang) => $"""
                                                    SELECT m."Id"
                                                         , m."ElbWordId"
                                                         , m."HebRefId"
                                                         , m."StepWordId"
                                                         , m."StrongsNumber"
                                                         , m."IsAddedWord"
                                                         , m."ParentGermanWordId"
                                                         , m."GermanWordPart"
                                                         , m."Role"
                                                    FROM "unshackled-word"."{Table(lang)}" m
                                                        JOIN "unshackled-word"."Elb1871Words" ew ON ew."Id" = m."ElbWordId"
                                                    WHERE 1=1
                                                        AND ew."BibleBookId" = @BookId
                                                    ORDER BY m."Id"
                                                    """;

    public const string GreekSources = """
                                       SELECT sgw."Id"
                                            , sgw."Morphology"
                                       FROM "unshackled-word"."StepGreekWords" sgw
                                       WHERE 1=1
                                           AND sgw."Id" IN (SELECT m."StepWordId"
                                                            FROM "unshackled-word"."Elb1871GreekMapping" m
                                                                JOIN "unshackled-word"."Elb1871Words" ew ON ew."Id" = m."ElbWordId"
                                                            WHERE 1=1
                                                                AND ew."BibleBookId" = @BookId)
                                       """;

    /// <summary>Morpheme tags as the mapping editor reads them (ExpandedStrongTags, else DisambiguatedStrongs).</summary>
    public const string HebrewSources = """
                                        SELECT shw."Id"
                                             , shw."Grammar" "Morphology"
                                             , COALESCE(NULLIF(shw."ExpandedStrongTags", ''), shw."DisambiguatedStrongs") "MorphemeTags"
                                        FROM "unshackled-word"."StepHebrewWords" shw
                                        WHERE 1=1
                                            AND shw."Id" IN (SELECT m."StepWordId"
                                                             FROM "unshackled-word"."Elb1871HebrewMapping" m
                                                                 JOIN "unshackled-word"."Elb1871Words" ew ON ew."Id" = m."ElbWordId"
                                                             WHERE 1=1
                                                                 AND ew."BibleBookId" = @BookId)
                                        """;

    public const string KnownLemmas = """
                                      SELECT DISTINCT lower(em."Lemma")
                                      FROM "unshackled-word"."Elb1871Morphology" em
                                      WHERE 1=1
                                          AND em."Lemma" IS NOT NULL
                                          AND em."Lemma" <> ''
                                      """;

    public const string ActiveAgentRuns = """
                                          SELECT count(*)
                                          FROM "unshackled-word"."AgentRuns" ar
                                          WHERE 1=1
                                              AND ar."Status" IN ('queued', 'running', 'waiting-for-provider')
                                          """;

    public const string Applied = """
                                  SELECT cs."Id"
                                       , cs."UserId"
                                       , cs."CreatedAt"
                                       , (SELECT count(*)::int4 FROM "unshackled-word"."MappingChangeSetRows" r WHERE r."ChangeSetId" = cs."Id") "MappingRows"
                                       , cs."WordCount" "LemmaRows"
                                  FROM "unshackled-word"."Elb1871MorphologyChangeSets" cs
                                  WHERE 1=1
                                      AND cs."Kind" = @Kind
                                      AND cs."RevertedAt" IS NULL
                                  ORDER BY cs."Id" DESC
                                  LIMIT 1
                                  """;

    /// <summary><c>DqRefreshState.LinkRolesAppliedAt</c> is added by the stats migration (0032); the job works without it.</summary>
    public const string AppliedAtColumnExists = """
                                                SELECT EXISTS (SELECT 1
                                                               FROM information_schema.columns c
                                                               WHERE 1=1
                                                                   AND c.table_schema = 'unshackled-word'
                                                                   AND c.table_name = 'DqRefreshState'
                                                                   AND c.column_name = 'LinkRolesAppliedAt')
                                                """;

    public const string ReadAppliedAt = """
                                        SELECT s."LinkRolesAppliedAt"
                                        FROM "unshackled-word"."DqRefreshState" s
                                        WHERE 1=1
                                            AND s."Id" = 1
                                        """;

    public const string SetAppliedAtNow = """
                                          UPDATE "unshackled-word"."DqRefreshState"
                                          SET "LinkRolesAppliedAt" = now()
                                          WHERE 1=1
                                              AND "Id" = 1
                                          """;

    /// <summary>Puts back the value the apply found (kept in the change set's Scope).</summary>
    public const string RestoreAppliedAt = """
                                           UPDATE "unshackled-word"."DqRefreshState"
                                           SET "LinkRolesAppliedAt" = (SELECT (cs."Scope" ->> 'previousLinkRolesAppliedAt')::timestamptz
                                                                       FROM "unshackled-word"."Elb1871MorphologyChangeSets" cs
                                                                       WHERE 1=1
                                                                           AND cs."Id" = @ChangeSetId)
                                           WHERE 1=1
                                               AND "Id" = 1
                                           """;

    public const string InsertChangeSet = """
                                          INSERT INTO "unshackled-word"."Elb1871MorphologyChangeSets" ("UserId", "Kind", "Scope", "Note", "WordCount", "RevertOf")
                                          VALUES (@UserId, @Kind, jsonb_build_object('mappingRows', @MappingRows, 'previousLinkRolesAppliedAt', @Previous::timestamptz), @Note, @WordCount, @RevertOf)
                                          RETURNING "Id"
                                          """;

    public const string InsertMappingRows = """
                                            INSERT INTO "unshackled-word"."MappingChangeSetRows" ("ChangeSetId", "Lang", "MappingId", "HebRefId", "ElbWordId", "Rule", "BeforeRole", "BeforeStrongsNumber", "BeforeIsAddedWord", "BeforeParentGermanWordId", "BeforeStepWordId", "AfterRole", "AfterStrongsNumber", "AfterIsAddedWord", "AfterParentGermanWordId", "AfterStepWordId")
                                            SELECT @ChangeSetId
                                                 , u.*
                                            FROM unnest(@Lang::text[], @MappingId::int4[], @HebRefId::int4[], @ElbWordId::int4[], @Rule::text[], @BeforeRole::text[], @BeforeStrongsNumber::text[], @BeforeIsAddedWord::boolean[], @BeforeParentGermanWordId::int4[], @BeforeStepWordId::int4[], @AfterRole::text[], @AfterStrongsNumber::text[], @AfterIsAddedWord::boolean[], @AfterParentGermanWordId::int4[], @AfterStepWordId::int4[]) AS u
                                            """;

    /// <summary>Writes the after values where the row still has its before values; the caller compares the count.</summary>
    public static string ApplyMapping(char lang) => $"""
                                                     UPDATE "unshackled-word"."{Table(lang)}" m
                                                     SET "Role" = r."AfterRole"
                                                       , "StrongsNumber" = r."AfterStrongsNumber"
                                                       , "IsAddedWord" = r."AfterIsAddedWord"
                                                       , "ParentGermanWordId" = r."AfterParentGermanWordId"
                                                       , "StepWordId" = r."AfterStepWordId"
                                                     FROM "unshackled-word"."MappingChangeSetRows" r
                                                     WHERE 1=1
                                                         AND r."ChangeSetId" = @ChangeSetId
                                                         AND r."Lang" = @Lang
                                                         AND m."Id" = r."MappingId"
                                                         AND m."Role" = r."BeforeRole"
                                                         AND m."StrongsNumber" IS NOT DISTINCT FROM r."BeforeStrongsNumber"
                                                         AND m."IsAddedWord" IS NOT DISTINCT FROM r."BeforeIsAddedWord"
                                                         AND m."ParentGermanWordId" IS NOT DISTINCT FROM r."BeforeParentGermanWordId"
                                                         AND m."StepWordId" IS NOT DISTINCT FROM r."BeforeStepWordId"
                                                     """;

    /// <summary>Writes the before values back where the row still has its after values (rows edited since are skipped).</summary>
    public static string UndoMapping(char lang) => $"""
                                                    UPDATE "unshackled-word"."{Table(lang)}" m
                                                    SET "Role" = r."BeforeRole"
                                                      , "StrongsNumber" = r."BeforeStrongsNumber"
                                                      , "IsAddedWord" = r."BeforeIsAddedWord"
                                                      , "ParentGermanWordId" = r."BeforeParentGermanWordId"
                                                      , "StepWordId" = r."BeforeStepWordId"
                                                    FROM "unshackled-word"."MappingChangeSetRows" r
                                                    WHERE 1=1
                                                        AND r."ChangeSetId" = @ChangeSetId
                                                        AND r."Lang" = @Lang
                                                        AND m."Id" = r."MappingId"
                                                        AND m."Role" = r."AfterRole"
                                                        AND m."StrongsNumber" IS NOT DISTINCT FROM r."AfterStrongsNumber"
                                                        AND m."IsAddedWord" IS NOT DISTINCT FROM r."AfterIsAddedWord"
                                                        AND m."ParentGermanWordId" IS NOT DISTINCT FROM r."AfterParentGermanWordId"
                                                        AND m."StepWordId" IS NOT DISTINCT FROM r."AfterStepWordId"
                                                    """;

    public const string CountMappingRows = """
                                           SELECT count(*)
                                           FROM "unshackled-word"."MappingChangeSetRows" r
                                           WHERE 1=1
                                               AND r."ChangeSetId" = @ChangeSetId
                                           """;

    public const string CountLemmaRows = """
                                         SELECT count(*)
                                         FROM "unshackled-word"."Elb1871MorphologyEdits" e
                                         WHERE 1=1
                                             AND e."ChangeSetId" = @ChangeSetId
                                         """;

    /// <summary>Lemma changes as Elb1871MorphologyEdits of the change set, with full before/after values like the bulk lemma edit.</summary>
    public const string ApplyLemmas = """
                                      WITH "Input" AS
                                      (
                                          SELECT i.*
                                          FROM unnest(@ElbWordIds::int4[], @HebRefIds::int4[], @Positions::int4[], @OldLemmas::text[], @NewLemmas::text[]) AS i("ElbWordId", "HebRefId", "PositionInVerse", "OldLemma", "NewLemma")
                                      )
                                      , "Updated" AS
                                      (
                                          UPDATE "unshackled-word"."Elb1871Morphology" m
                                          SET "Lemma" = i."NewLemma"
                                          FROM "Input" i
                                          WHERE 1=1
                                              AND m."HebRefId" = i."HebRefId"
                                              AND m."PositionInVerse" = i."PositionInVerse"
                                              AND m."Lemma" IS NOT DISTINCT FROM i."OldLemma"
                                          RETURNING m."HebRefId", m."PositionInVerse", m."PartOfSpeech", m."Stts", m."Degree", m."VerbForm", m."Tense", m."Person", m."Number", m."Mood", m."Case", m."Gender", i."ElbWordId", i."OldLemma", i."NewLemma"
                                      )
                                      , "Audited" AS
                                      (
                                          INSERT INTO "unshackled-word"."Elb1871MorphologyEdits" ("ElbWordId", "HebRefId", "PositionInVerse", "UserId", "ChangeSetId", "BeforeValues", "AfterValues")
                                          SELECT u."ElbWordId"
                                               , u."HebRefId"
                                               , u."PositionInVerse"
                                               , @UserId
                                               , @ChangeSetId
                                               , jsonb_build_object('Lemma', u."OldLemma", 'PartOfSpeech', u."PartOfSpeech", 'Stts', u."Stts", 'Degree', u."Degree", 'VerbForm', u."VerbForm", 'Tense', u."Tense", 'Person', u."Person", 'Number', u."Number", 'Mood', u."Mood", 'Case', u."Case", 'Gender', u."Gender")
                                               , jsonb_build_object('Lemma', u."NewLemma", 'PartOfSpeech', u."PartOfSpeech", 'Stts', u."Stts", 'Degree', u."Degree", 'VerbForm', u."VerbForm", 'Tense', u."Tense", 'Person', u."Person", 'Number', u."Number", 'Mood', u."Mood", 'Case', u."Case", 'Gender', u."Gender")
                                          FROM "Updated" u
                                          RETURNING 1
                                      )
                                      SELECT count(*)
                                      FROM "Audited"
                                      """;

    /// <summary>Restores the lemma of every edit of the change set whose row still has the after lemma; audited under the undo set.</summary>
    public const string UndoLemmas = """
                                     WITH "Candidates" AS
                                     (
                                         SELECT m."Id"                         "MorphologyId"
                                              , e."ElbWordId"
                                              , e."BeforeValues" ->> 'Lemma'   "RestoredLemma"
                                              , m."Lemma"                      "CurrentLemma"
                                         FROM "unshackled-word"."Elb1871MorphologyEdits" e
                                             JOIN "unshackled-word"."Elb1871Morphology" m ON m."HebRefId" = e."HebRefId" AND m."PositionInVerse" = e."PositionInVerse"
                                         WHERE 1=1
                                             AND e."ChangeSetId" = @ChangeSetId
                                             AND m."Lemma" IS NOT DISTINCT FROM e."AfterValues" ->> 'Lemma'
                                         ORDER BY m."Id"
                                         FOR UPDATE OF m
                                     )
                                     , "Updated" AS
                                     (
                                         UPDATE "unshackled-word"."Elb1871Morphology" m
                                         SET "Lemma" = c."RestoredLemma"
                                         FROM "Candidates" c
                                         WHERE 1=1
                                             AND m."Id" = c."MorphologyId"
                                         RETURNING m."HebRefId", m."PositionInVerse", m."PartOfSpeech", m."Stts", m."Degree", m."VerbForm", m."Tense", m."Person", m."Number", m."Mood", m."Case", m."Gender", c."ElbWordId", c."CurrentLemma", c."RestoredLemma"
                                     )
                                     , "Audited" AS
                                     (
                                         INSERT INTO "unshackled-word"."Elb1871MorphologyEdits" ("ElbWordId", "HebRefId", "PositionInVerse", "UserId", "ChangeSetId", "BeforeValues", "AfterValues")
                                         SELECT u."ElbWordId"
                                              , u."HebRefId"
                                              , u."PositionInVerse"
                                              , @UserId
                                              , @UndoChangeSetId
                                              , jsonb_build_object('Lemma', u."CurrentLemma", 'PartOfSpeech', u."PartOfSpeech", 'Stts', u."Stts", 'Degree', u."Degree", 'VerbForm', u."VerbForm", 'Tense', u."Tense", 'Person', u."Person", 'Number', u."Number", 'Mood', u."Mood", 'Case', u."Case", 'Gender', u."Gender")
                                              , jsonb_build_object('Lemma', u."RestoredLemma", 'PartOfSpeech', u."PartOfSpeech", 'Stts', u."Stts", 'Degree', u."Degree", 'VerbForm', u."VerbForm", 'Tense', u."Tense", 'Person', u."Person", 'Number', u."Number", 'Mood', u."Mood", 'Case', u."Case", 'Gender', u."Gender")
                                         FROM "Updated" u
                                         RETURNING 1
                                     )
                                     SELECT count(*)
                                     FROM "Audited"
                                     """;

    /// <summary>Keeps Elb1871Verses.LemmaText (and the verse search vector) in step with the lemmas of the change set.</summary>
    public const string RefreshLemmaText = """
                                           SELECT "unshackled-word"."RefreshVerseLemmaText"(ARRAY(SELECT DISTINCT e."HebRefId"
                                                                                                FROM "unshackled-word"."Elb1871MorphologyEdits" e
                                                                                                WHERE 1=1
                                                                                                    AND e."ChangeSetId" = @ChangeSetId))
                                           """;

    public const string LockChangeSet = """
                                        SELECT cs."Kind"
                                             , cs."RevertedAt"
                                        FROM "unshackled-word"."Elb1871MorphologyChangeSets" cs
                                        WHERE 1=1
                                            AND cs."Id" = @ChangeSetId
                                        FOR UPDATE
                                        """;

    public const string FinishUndo = """
                                     UPDATE "unshackled-word"."Elb1871MorphologyChangeSets"
                                     SET "WordCount" = @Restored
                                     WHERE 1=1
                                         AND "Id" = @UndoChangeSetId;
                                     UPDATE "unshackled-word"."Elb1871MorphologyChangeSets"
                                     SET "RevertedAt" = now()
                                     WHERE 1=1
                                         AND "Id" = @ChangeSetId
                                     """;

    public static string Table(char lang) => lang == MappingLang.Hebrew ? "Elb1871HebrewMapping" : "Elb1871GreekMapping";
}
