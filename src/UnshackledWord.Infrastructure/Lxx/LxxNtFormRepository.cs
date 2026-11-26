using Dapper;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Features.Lxx;

namespace UnshackledWord.Infrastructure.Lxx;

/// <summary>Dapper bulk reads and the transactional write of LXX pass 1 (roadmap 0028.6). The SQL is public for the database tests.</summary>
public sealed class LxxNtFormRepository : ILxxNtFormRepository
{
    private readonly IDbConnectionFactory _factory;

    public LxxNtFormRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<IReadOnlyList<NtFormAnalysis>> GetNtAnalysesAsync(CancellationToken token = default)
    {
        using var connection = await _factory.CreateDbConnectionAsync(token);
        return (await connection.QueryAsync<NtFormAnalysis>(new CommandDefinition(LxxNtFormSql.NtAnalyses, commandTimeout: 300, cancellationToken: token))).ToList();
    }

    public async Task<IReadOnlyList<LxxPrefillWord>> GetLxxWordsAsync(CancellationToken token = default)
    {
        using var connection = await _factory.CreateDbConnectionAsync(token);
        return (await connection.QueryAsync<LxxPrefillWord>(new CommandDefinition(LxxNtFormSql.Words, commandTimeout: 300, cancellationToken: token))).ToList();
    }

    public async Task<IReadOnlyList<LxxExistingTag>> GetTagsAsync(CancellationToken token = default)
    {
        using var connection = await _factory.CreateDbConnectionAsync(token);
        return (await connection.QueryAsync<LxxExistingTag>(new CommandDefinition(LxxNtFormSql.Tags, commandTimeout: 300, cancellationToken: token))).ToList();
    }

    public async Task<LxxStatusCounts> GetStatusCountsAsync(CancellationToken token = default)
    {
        using var connection = await _factory.CreateDbConnectionAsync(token);
        using var grid = await connection.QueryMultipleAsync(new CommandDefinition(LxxNtFormSql.StatusCounts, cancellationToken: token));
        var totals = await grid.ReadSingleAsync<(int Verses, int Words)>();
        var tags = (await grid.ReadAsync<LxxTagCount>()).ToList();
        return new LxxStatusCounts(totals.Verses, totals.Words, tags);
    }

    public async Task<LxxNtFormApplyResult> ApplyAsync(IReadOnlyList<LxxNtFormProposal> proposals, CancellationToken token = default)
    {
        var parameters = new
        {
            Ids = proposals.Select(p => p.LxxWordId).ToArray(),
            Lemmas = proposals.Select(p => p.Lemma).ToArray(),
            Strongs = proposals.Select(p => p.Strongs).ToArray(),
            Morphologies = proposals.Select(p => p.Morphology).ToArray(),
        };

        using var connection = await _factory.CreateDbConnectionAsync(token);
        using var transaction = connection.BeginTransaction();

        // Deletes first (rows no longer proposed), then upsert; both only ever touch nt-form/proposed rows.
        var deleted = await connection.ExecuteAsync(new CommandDefinition(LxxNtFormSql.DeleteStale, parameters, transaction, commandTimeout: 300, cancellationToken: token));
        var changes = (await connection.QueryAsync<bool>(new CommandDefinition(LxxNtFormSql.Upsert, parameters, transaction, commandTimeout: 300, cancellationToken: token))).ToList();
        transaction.Commit();

        var inserted = changes.Count(isInsert => isInsert);
        return new LxxNtFormApplyResult(inserted, changes.Count - inserted, deleted);
    }
}

/// <summary>The SQL of <see cref="LxxNtFormRepository"/>.</summary>
public static class LxxNtFormSql
{
    public const string NtAnalyses = """
                                     SELECT w."Greek"
                                          , w."Lemma"
                                          , w."DisambiguatedStrongs" AS "Strongs"
                                          , w."Morphology"
                                     FROM "unshackled-word"."StepGreekWords" w
                                     WHERE 1=1
                                     GROUP BY w."Greek", w."Lemma", w."DisambiguatedStrongs", w."Morphology"
                                     """;

    public const string Words = """
                                SELECT w."Id"
                                     , v."BibleBookId"
                                     , v."TextForm"
                                     , w."Greek"
                                FROM "unshackled-word"."LxxWords" w
                                JOIN "unshackled-word"."LxxVerses" v ON v."Id" = w."LxxVerseId"
                                WHERE 1=1
                                ORDER BY w."Id"
                                """;

    public const string Tags = """
                               SELECT t."LxxWordId"
                                    , t."Status"
                                    , t."Source"
                               FROM "unshackled-word"."LxxWordTags" t
                               WHERE 1=1
                               """;

    /// <summary>Two result sets: (Verses, Words) and the tag counts per (Status, Source).</summary>
    public const string StatusCounts = """
                                       SELECT (SELECT count(*) FROM "unshackled-word"."LxxVerses")::int4 AS "Verses"
                                            , (SELECT count(*) FROM "unshackled-word"."LxxWords")::int4 AS "Words";

                                       SELECT t."Status"
                                            , t."Source"
                                            , count(*)::int4 AS "Count"
                                       FROM "unshackled-word"."LxxWordTags" t
                                       WHERE 1=1
                                       GROUP BY t."Status", t."Source"
                                       ORDER BY t."Status", t."Source"
                                       """;

    /// <summary>Parameters Ids, Lemmas, Strongs, Morphologies (parallel arrays). Returns one bool per written row: true = inserted, false = updated.</summary>
    public const string Upsert = """
                                 INSERT INTO "unshackled-word"."LxxWordTags" ("LxxWordId", "Lemma", "Strongs", "Morphology", "Status", "Source", "UserId", "UpdatedAt")
                                 SELECT p."LxxWordId"
                                      , p."Lemma"
                                      , p."Strongs"
                                      , p."Morphology"
                                      , 'proposed'
                                      , 'nt-form'
                                      , NULL
                                      , now()
                                 FROM unnest(@Ids, @Lemmas, @Strongs, @Morphologies) AS p ("LxxWordId", "Lemma", "Strongs", "Morphology")
                                 ON CONFLICT ("LxxWordId") DO UPDATE
                                 SET "Lemma" = EXCLUDED."Lemma"
                                   , "Strongs" = EXCLUDED."Strongs"
                                   , "Morphology" = EXCLUDED."Morphology"
                                   , "UpdatedAt" = now()
                                 WHERE 1=1
                                     AND "LxxWordTags"."Source" = 'nt-form'
                                     AND "LxxWordTags"."Status" = 'proposed'
                                     AND ("LxxWordTags"."Lemma", "LxxWordTags"."Strongs", "LxxWordTags"."Morphology")
                                         IS DISTINCT FROM (EXCLUDED."Lemma", EXCLUDED."Strongs", EXCLUDED."Morphology")
                                 RETURNING (xmax = 0) AS "Inserted"
                                 """;

    /// <summary>Parameter Ids: deletes nt-form proposals of words that no longer get one.</summary>
    public const string DeleteStale = """
                                      DELETE FROM "unshackled-word"."LxxWordTags" t
                                      WHERE 1=1
                                          AND t."Source" = 'nt-form'
                                          AND t."Status" = 'proposed'
                                          AND t."LxxWordId" <> ALL (@Ids)
                                      """;
}
