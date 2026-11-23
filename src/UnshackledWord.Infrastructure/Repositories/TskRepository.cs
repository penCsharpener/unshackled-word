using UnshackledWord.Application.Abstractions;
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
        // hiding and ordering happen in CrossReferenceCuration.
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
                           FROM links l
                           LEFT JOIN "unshackled-word"."TskCurations" c
                               ON c."SourceHebRefId" = l."SourceHebRefId"
                               AND c."TargetStartHebRefId" = l."TargetStartHebRefId"
                               AND COALESCE(c."TargetEndHebRefId", 0) = COALESCE(l."TargetEndHebRefId", 0)
                           ORDER BY l."SourceHebRefId", l."Part", l."Lxx", l."Sort" NULLS LAST, l."Id"
                           LIMIT @Limit
                           """;

        var rows = await _dbReader.ReadAsListAsync<TskCrossReference>(sql, new { StartRefId = startHebRefId, EndRefId = endHebRefId, Limit = limit });
        return rows.ToList();
    }
}
