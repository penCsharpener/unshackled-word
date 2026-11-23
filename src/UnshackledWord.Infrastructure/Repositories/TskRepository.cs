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
        // The curation of each link (roadmap 0024.3) joins on the key (index "TskCurations_Key_UX"); hiding and ordering happen in CrossReferenceCuration.
        const string sql = """
                           SELECT t."SourceHebRefId"
                                , t."Scope"
                                , t."TargetStartHebRefId"
                                , t."TargetEndHebRefId"
                                , c."Rating"
                                , c."Comment"
                           FROM "unshackled-word"."Tsk" t
                           LEFT JOIN "unshackled-word"."TskCurations" c
                               ON c."SourceHebRefId" = t."SourceHebRefId"
                               AND c."TargetStartHebRefId" = t."TargetStartHebRefId"
                               AND COALESCE(c."TargetEndHebRefId", 0) = COALESCE(t."TargetEndHebRefId", 0)
                           WHERE 1=1
                               AND t."SourceHebRefId" BETWEEN @StartRefId AND @EndRefId
                           ORDER BY t."SourceHebRefId", t."LxxRefId", t."Sort" NULLS LAST, t."Id"
                           LIMIT @Limit
                           """;

        var rows = await _dbReader.ReadAsListAsync<TskCrossReference>(sql, new { StartRefId = startHebRefId, EndRefId = endHebRefId, Limit = limit });
        return rows.ToList();
    }
}
