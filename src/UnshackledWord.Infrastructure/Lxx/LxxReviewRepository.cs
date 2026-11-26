using System.Data;
using Dapper;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Features.AgentMapping;
using UnshackledWord.Application.Features.AgentMapping.Lxx;
using UnshackledWord.Application.Features.Lxx.Review;

namespace UnshackledWord.Infrastructure.Lxx;

/// <summary>The LXX review page (roadmap 0028.7f): open work per chapter, the state of units, and the one transactional write path. SQL in <see cref="LxxReviewSql"/>.</summary>
public sealed class LxxReviewRepository : ILxxReviewRepository
{
    private readonly IDbConnectionFactory _factory;

    public LxxReviewRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<IReadOnlyList<LxxReviewChapterCountRow>> GetChapterCountsAsync(CancellationToken token = default)
    {
        using var connection = await _factory.CreateDbConnectionAsync(token);
        var rows = await connection.QueryAsync<LxxReviewChapterCountRow>(new CommandDefinition(LxxReviewSql.ChapterCounts, commandTimeout: 120, cancellationToken: token));

        return rows.ToList();
    }

    public async Task<LxxReviewState> GetChapterAsync(int bibleBookId, int chapter, CancellationToken token = default)
    {
        using var connection = await _factory.CreateDbConnectionAsync(token);
        var units = (await connection.QueryAsync<LxxAgentUnitRow>(new CommandDefinition(LxxReviewSql.ChapterUnits, new { BibleBookId = bibleBookId, Chapter = chapter }, cancellationToken: token))).ToList();

        return await ReadStateAsync(connection, null, units, token);
    }

    public async Task<LxxReviewApplied<T>> ApplyAsync<T>(IReadOnlyCollection<int> lxxVerseIds, Func<LxxReviewState, LxxReviewPlan<T>> plan, int userId, CancellationToken token = default)
    {
        using var connection = await _factory.CreateDbConnectionAsync(token);
        using var transaction = connection.BeginTransaction();

        var result = await ApplyAsync(connection, transaction, lxxVerseIds, plan, userId, token);
        if (result.Conflict)
        {
            transaction.Rollback();
        }
        else
        {
            transaction.Commit();
        }

        return result;
    }

    /// <summary>The write path inside a caller's transaction (the database tests roll it back). The caller commits unless <see cref="LxxReviewApplied{T}.Conflict"/>.</summary>
    public static async Task<LxxReviewApplied<T>> ApplyAsync<T>(IDbConnection connection, IDbTransaction transaction, IReadOnlyCollection<int> lxxVerseIds,
        Func<LxxReviewState, LxxReviewPlan<T>> plan, int userId, CancellationToken token = default)
    {
        var ids = lxxVerseIds.Distinct().Order().ToArray();
        // Lock the units first: two confirms of the same verse run one after the other, and the second sees the first one's result.
        await connection.ExecuteAsync(new CommandDefinition(LxxReviewSql.LockUnits, new { Ids = ids }, transaction, cancellationToken: token));
        var units = (await connection.QueryAsync<LxxAgentUnitRow>(new CommandDefinition(LxxAgentSql.UnitsById, new { Ids = ids }, transaction, cancellationToken: token))).ToList();
        var state = await ReadStateAsync(connection, transaction, units, token);

        var decided = plan(state);
        if (decided.Write is not { IsEmpty: false } write)
        {
            return new LxxReviewApplied<T>(decided.Result, false);
        }

        if (write.ProposalCloses.Count > 0)
        {
            var closed = await connection.ExecuteAsync(new CommandDefinition(LxxReviewSql.CloseProposals, new
            {
                Ids = write.ProposalCloses.Select(c => c.Id).ToArray(),
                Statuses = write.ProposalCloses.Select(c => c.Status).ToArray(),
                UserId = userId,
            }, transaction, cancellationToken: token));
            if (closed != write.ProposalCloses.Count)
            {
                return new LxxReviewApplied<T>(decided.Result, true);
            }
        }

        if (write.Tags.Count > 0)
        {
            await connection.ExecuteAsync(new CommandDefinition(LxxReviewSql.UpsertConfirmedTags, new
            {
                Ids = write.Tags.Select(t => t.LxxWordId).ToArray(),
                Lemmas = write.Tags.Select(t => t.Lemma).ToArray(),
                Strongs = write.Tags.Select(t => t.Strongs).ToArray(),
                Morphologies = write.Tags.Select(t => t.Morphology).ToArray(),
                Sources = write.Tags.Select(t => t.Source).ToArray(),
                UserId = userId,
            }, transaction, cancellationToken: token));
        }

        if (write.LinkWordIds.Count > 0)
        {
            await connection.ExecuteAsync(new CommandDefinition(LxxReviewSql.DeleteLinks, new { Ids = write.LinkWordIds.Distinct().ToArray() }, transaction, cancellationToken: token));
            var links = write.Links.Distinct().ToList();
            if (links.Count > 0)
            {
                await connection.ExecuteAsync(new CommandDefinition(LxxReviewSql.InsertConfirmedLinks, new
                {
                    WordIds = links.Select(l => l.LxxWordId).ToArray(),
                    HebrewIds = links.Select(l => l.StepHebrewWordId).ToArray(),
                    UserId = userId,
                }, transaction, cancellationToken: token));
            }
        }

        foreach (var audit in write.Audits)
        {
            await connection.ExecuteAsync(new CommandDefinition(LxxReviewSql.InsertAudit, new
            {
                audit.LxxVerseId,
                audit.Action,
                Before = audit.BeforeJson,
                After = audit.AfterJson,
                UserId = userId,
            }, transaction, cancellationToken: token));
        }

        return new LxxReviewApplied<T>(decided.Result, false);
    }

    private static async Task<LxxReviewState> ReadStateAsync(IDbConnection connection, IDbTransaction? transaction, List<LxxAgentUnitRow> units, CancellationToken token)
    {
        if (units.Count == 0)
        {
            return new LxxReviewState();
        }

        var unitIds = units.Select(u => u.Id).ToArray();
        var words = (await connection.QueryAsync<LxxAgentWordRow>(new CommandDefinition(LxxAgentSql.Words, new { Ids = unitIds }, transaction, cancellationToken: token))).ToList();
        var hebRefIds = units.Where(u => u.HebRefId is not null).Select(u => u.HebRefId!.Value).Distinct().ToArray();
        var hebrew = hebRefIds.Length == 0
            ? []
            : (await connection.QueryAsync<AgentSourceWordRow>(new CommandDefinition(LxxAgentSql.HebrewWords, new { HebRefIds = hebRefIds }, transaction, cancellationToken: token))).ToList();
        var links = (await connection.QueryAsync<LxxReviewLinkRow>(new CommandDefinition(LxxReviewSql.Links, new { Ids = words.Select(w => w.Id).ToArray() }, transaction, cancellationToken: token))).ToList();
        var proposals = (await connection.QueryAsync<LxxReviewProposalRow>(new CommandDefinition(LxxReviewSql.OpenProposals, new { Ids = unitIds }, transaction, cancellationToken: token))).ToList();

        return new LxxReviewState { Units = units, Words = words, HebrewWords = hebrew, Links = links, Proposals = proposals };
    }
}

/// <summary>The SQL of <see cref="LxxReviewRepository"/>; public for the database tests.</summary>
public static class LxxReviewSql
{
    /// <summary>Per book and Rahlfs chapter: units with a proposed tag or an open AI proposal, units with an AI proposal, low-confidence AI words.</summary>
    public const string ChapterCounts = """
                                        WITH proposed AS (
                                            SELECT DISTINCT w."LxxVerseId"
                                            FROM "unshackled-word"."LxxWordTags" t
                                                JOIN "unshackled-word"."LxxWords" w ON w."Id" = t."LxxWordId"
                                            WHERE 1=1
                                                AND t."Status" = 'proposed'
                                        ), ai AS (
                                            SELECT (p."Flags" ->> 'lxxVerseId')::int4 AS "LxxVerseId"
                                                 , (SELECT count(*)
                                                    FROM jsonb_array_elements(COALESCE(p."Lemmas", '[]'::jsonb)) e
                                                    WHERE 1=1
                                                        AND e ->> 'confidence' = 'low')::int4 AS "Low"
                                            FROM "unshackled-word"."AgentVerseProposals" p
                                            WHERE 1=1
                                                AND p."Language" = 'lxx'
                                                AND p."Status" = 'open'
                                                AND jsonb_typeof(p."Flags") = 'object'
                                        )
                                        SELECT v."BibleBookId"
                                             , v."Chapter"
                                             , count(*)::int4 AS "OpenUnits"
                                             , count(ai."LxxVerseId")::int4 AS "AiUnits"
                                             , COALESCE(sum(ai."Low"), 0)::int4 AS "LowWords"
                                        FROM "unshackled-word"."LxxVerses" v
                                            LEFT JOIN proposed pr ON pr."LxxVerseId" = v."Id"
                                            LEFT JOIN ai ON ai."LxxVerseId" = v."Id"
                                        WHERE 1=1
                                            AND (pr."LxxVerseId" IS NOT NULL OR ai."LxxVerseId" IS NOT NULL)
                                        GROUP BY v."BibleBookId"
                                               , v."Chapter"
                                        ORDER BY v."BibleBookId"
                                               , v."Chapter"
                                        """;

    /// <summary>Every unit of a Rahlfs chapter (subverses, additions, both Daniel forms), Swete order; the columns of <see cref="LxxAgentUnitRow"/>.</summary>
    public const string ChapterUnits = """
                                       SELECT l."Id"
                                            , l."BibleBookId"
                                            , l."Chapter"
                                            , l."Verse"
                                            , l."Subverse"
                                            , l."TextForm"
                                            , l."HebRefId"
                                            , l."MapKind"
                                            , l."SortOrder"
                                       FROM "unshackled-word"."LxxVerses" l
                                       WHERE 1=1
                                           AND l."BibleBookId" = @BibleBookId
                                           AND l."Chapter" = @Chapter
                                       ORDER BY l."SortOrder"
                                       """;

    public const string LockUnits = """
                                    SELECT l."Id"
                                    FROM "unshackled-word"."LxxVerses" l
                                    WHERE 1=1
                                        AND l."Id" = ANY(@Ids)
                                    ORDER BY l."Id"
                                    FOR UPDATE
                                    """;

    public const string Links = """
                                SELECT k."LxxWordId"
                                     , k."StepHebrewWordId"
                                     , k."Status"
                                FROM "unshackled-word"."LxxHebrewLinks" k
                                WHERE 1=1
                                    AND k."LxxWordId" = ANY(@Ids)
                                ORDER BY k."LxxWordId"
                                       , k."StepHebrewWordId"
                                """;

    /// <summary>Open LXX proposals of the units (<c>Flags.lxxVerseId</c>, written by the LXX submit path).</summary>
    public const string OpenProposals = """
                                        SELECT p."Id"
                                             , p."RunId"
                                             , (p."Flags" ->> 'lxxVerseId')::int4 AS "LxxVerseId"
                                             , p."Lemmas"::text AS "LemmasJson"
                                             , p."Links"::text AS "LinksJson"
                                             , p."Flags"::text AS "FlagsJson"
                                             , p."Note"
                                             , p."CreatedAt"
                                        FROM "unshackled-word"."AgentVerseProposals" p
                                        WHERE 1=1
                                            AND p."Language" = 'lxx'
                                            AND p."Status" = 'open'
                                            AND jsonb_typeof(p."Flags") = 'object'
                                            AND (p."Flags" ->> 'lxxVerseId')::int4 = ANY(@Ids)
                                        ORDER BY p."Id"
                                        """;

    /// <summary>Parallel arrays Ids, Lemmas, Strongs, Morphologies, Sources; every row becomes confirmed with the reviewer as user.</summary>
    public const string UpsertConfirmedTags = """
                                              INSERT INTO "unshackled-word"."LxxWordTags" ("LxxWordId", "Lemma", "Strongs", "Morphology", "Status", "Source", "UserId", "UpdatedAt")
                                              SELECT p."LxxWordId"
                                                   , p."Lemma"
                                                   , p."Strongs"
                                                   , p."Morphology"
                                                   , 'confirmed'
                                                   , p."Source"
                                                   , @UserId
                                                   , now()
                                              FROM unnest(@Ids, @Lemmas, @Strongs, @Morphologies, @Sources) AS p ("LxxWordId", "Lemma", "Strongs", "Morphology", "Source")
                                              ON CONFLICT ("LxxWordId") DO UPDATE
                                              SET "Lemma" = EXCLUDED."Lemma"
                                                , "Strongs" = EXCLUDED."Strongs"
                                                , "Morphology" = EXCLUDED."Morphology"
                                                , "Status" = 'confirmed'
                                                , "Source" = EXCLUDED."Source"
                                                , "UserId" = EXCLUDED."UserId"
                                                , "UpdatedAt" = now()
                                              """;

    public const string DeleteLinks = """
                                      DELETE FROM "unshackled-word"."LxxHebrewLinks" k
                                      WHERE 1=1
                                          AND k."LxxWordId" = ANY(@Ids)
                                      """;

    public const string InsertConfirmedLinks = """
                                               INSERT INTO "unshackled-word"."LxxHebrewLinks" ("LxxWordId", "StepHebrewWordId", "Status", "UserId", "UpdatedAt")
                                               SELECT p."LxxWordId"
                                                    , p."StepHebrewWordId"
                                                    , 'confirmed'
                                                    , @UserId
                                                    , now()
                                               FROM unnest(@WordIds, @HebrewIds) AS p ("LxxWordId", "StepHebrewWordId")
                                               """;

    public const string InsertAudit = """
                                      INSERT INTO "unshackled-word"."LxxTagAudit" ("LxxVerseId", "Action", "Before", "After", "UserId")
                                      VALUES (@LxxVerseId, @Action, CAST(@Before AS jsonb), CAST(@After AS jsonb), @UserId)
                                      """;

    /// <summary>Only open proposals are closed; the row count tells whether every one still was.</summary>
    public const string CloseProposals = """
                                         UPDATE "unshackled-word"."AgentVerseProposals" p
                                         SET "Status" = c."Status"
                                           , "ReviewedByUserId" = @UserId
                                           , "ReviewedAt" = now()
                                         FROM unnest(@Ids, @Statuses) AS c ("Id", "Status")
                                         WHERE 1=1
                                             AND p."Id" = c."Id"
                                             AND p."Status" = 'open'
                                         """;
}
