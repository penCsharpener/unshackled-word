using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Features.Notes;
using UnshackledWord.Infrastructure.Notes;
using UnshackledWord.Application.Repositories;
using UnshackledWord.Domain.Models.Study;

namespace UnshackledWord.Infrastructure.Repositories;

public sealed class TskRepository : ITskRepository
{
    private readonly IDbReader _dbReader;

    public TskRepository(IDbReader dbReader)
    {
        _dbReader = dbReader;
    }

    public async Task<IReadOnlyList<TskCrossReference>> GetOutgoingAsync(int startHebRefId, int endHebRefId, int limit, CancellationToken token = default)
    {
        // Keys in Elberfelder numbering and already normalised (migration 0044, TskKeySql). Rows of one source verse keep the TSK file order:
        // "LxxRefId" first (a few Hebrew verses take the TSK entries of two KJV verses), then "Sort" (NULL before a reseed), then "Id".
        // Added links (roadmap 0024.4, "TskCurations" with "Origin" 'added') follow the TSK rows of their verse in creation order; one whose key
        // TSK also has is shown once (as the TSK row). The curation of each link (roadmap 0024.3) joins on the key (index "TskCurations_Key_UX");
        // hiding and ordering happen in CrossReferenceCuration. A word link (roadmap 0024.5) carries the Elb1871 words now at its positions, so the
        // drift check (CrossReferenceWordLink) needs no second query.
        const string sql = """
                           WITH links AS (
                               SELECT t."SourceHebRefId"
                                    , t."Scope"
                                    , t."TargetStartHebRefId"
                                    , t."TargetEndHebRefId"
                                    , 0 AS "Part"
                                    , t."LxxRefId" AS "Lxx"
                                    , t."Sort"
                                    , t."Id"
                               FROM "unshackled-word"."Tsk" t
                               WHERE 1=1
                                   AND t."SourceHebRefId" BETWEEN @StartRefId AND @EndRefId
                               UNION ALL
                               SELECT a."SourceHebRefId"
                                    , ''
                                    , a."TargetStartHebRefId"
                                    , a."TargetEndHebRefId"
                                    , 1
                                    , 0
                                    , NULL
                                    , a."Id"
                               FROM "unshackled-word"."TskCurations" a
                               WHERE 1=1
                                   AND a."Origin" = 'added'
                                   AND a."SourceHebRefId" BETWEEN @StartRefId AND @EndRefId
                                   AND NOT EXISTS (
                                       SELECT 1
                                       FROM "unshackled-word"."Tsk" t
                                       WHERE 1=1
                                           AND t."SourceHebRefId" = a."SourceHebRefId"
                                           AND t."TargetStartHebRefId" = a."TargetStartHebRefId"
                                           AND t."TargetEndHebRefId" IS NOT DISTINCT FROM a."TargetEndHebRefId"
                                   )
                           )
                           SELECT l."SourceHebRefId"
                                , l."Scope"
                                , l."TargetStartHebRefId"
                                , l."TargetEndHebRefId"
                                , COALESCE(c."Origin", 'tsk') AS "Origin"
                                , c."Rating"
                                , c."Comment"
                                , c."StartPosition"
                                , c."EndPosition"
                                , c."QuotedText"
                                , cw."CurrentQuote"
                                , COALESCE(cw."CurrentWordCount", 0) AS "CurrentWordCount"
                           FROM links l
                           LEFT JOIN "unshackled-word"."TskCurations" c
                               ON c."SourceHebRefId" = l."SourceHebRefId"
                               AND c."TargetStartHebRefId" = l."TargetStartHebRefId"
                               AND COALESCE(c."TargetEndHebRefId", 0) = COALESCE(l."TargetEndHebRefId", 0)
                           LEFT JOIN LATERAL (
                               SELECT string_agg(w."WordInContext", ' ' ORDER BY w."PositionInVerse") AS "CurrentQuote"
                                    , count(*)::int4 AS "CurrentWordCount"
                               FROM "unshackled-word"."Elb1871Words" w
                               WHERE 1=1
                                   AND c."StartPosition" IS NOT NULL
                                   AND w."HebRefId" = c."SourceHebRefId"
                                   AND w."PositionInVerse" BETWEEN c."StartPosition" AND c."EndPosition"
                           ) cw ON c."StartPosition" IS NOT NULL
                           ORDER BY l."SourceHebRefId", l."Part", l."Lxx", l."Sort" NULLS LAST, l."Id"
                           LIMIT @Limit
                           """;

        var rows = await _dbReader.ReadAsListAsync<TskCrossReference>(sql, new { StartRefId = startHebRefId, EndRefId = endHebRefId, Limit = limit });
        return rows.ToList();
    }

    public async Task<IReadOnlyList<NoteVariantWordRow>> GetVariantWordsAsync(IReadOnlyCollection<int> hebRefIds, int level, CancellationToken token = default)
    {
        if (hebRefIds.Count == 0 || level <= 0)
        {
            return [];
        }

        // The notes' query (roadmap 0023.2): one variant per verse at this level, its words with their Elb1871 base position.
        var rows = await _dbReader.ReadAsListAsync<NoteVariantWordRow>(NoteSql.VariantWords, new
        {
            Ids = hebRefIds.ToArray(),
            Level = level,
            FromRefId = hebRefIds.Min(),
            ToRefId = hebRefIds.Max(),
        });
        return rows.ToList();
    }
}
