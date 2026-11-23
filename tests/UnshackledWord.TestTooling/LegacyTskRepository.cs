using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Repositories;
using UnshackledWord.Domain.Models.Study;

namespace UnshackledWord.TestTooling;

/// <summary>
/// The read path of <c>TskRepository.GetOutgoingAsync</c> before roadmap 0024.2 (per-request translation of the LxxRefId columns via
/// <c>BibleVerseCountingMapping</c>, raw ranges normalised later by <c>CrossReferenceMapper</c>). Kept only as the reference the stored keys
/// of migration 0044 are compared against. The copies 0044 makes for a source verse that maps to several Hebrew verses (rows whose
/// <c>SourceHebRefId</c> is above the lowest mapped verse) are left out, so the query sees the rows as they were before the migration.
/// </summary>
public sealed class LegacyTskRepository : ITskRepository
{
    private readonly IDbReader _dbReader;

    public LegacyTskRepository(IDbReader dbReader)
    {
        _dbReader = dbReader;
    }

    public async Task<IReadOnlyList<TskCrossReference>> GetOutgoingAsync(int startHebRefId, int endHebRefId, int limit, CancellationToken token = default)
    {
        const string sql = """
                           SELECT bvcm."HebRefId" AS "SourceHebRefId"
                                , t."Scope"
                                , COALESCE((SELECT MIN(s."HebRefId")
                                            FROM "unshackled-word"."BibleVerseCountingMapping" s
                                            WHERE s."LxxRefId" = t."RelatedStartLxxRefId"), t."RelatedStartLxxRefId") AS "TargetStartHebRefId"
                                , CASE WHEN t."RelatedEndLxxRefId" IS NULL THEN NULL
                                       ELSE COALESCE((SELECT MAX(e."HebRefId")
                                                      FROM "unshackled-word"."BibleVerseCountingMapping" e
                                                      WHERE e."LxxRefId" = t."RelatedEndLxxRefId"), t."RelatedEndLxxRefId") END AS "TargetEndHebRefId"
                           FROM "unshackled-word"."Tsk" t
                           INNER JOIN "unshackled-word"."BibleVerseCountingMapping" bvcm ON bvcm."LxxRefId" = t."LxxRefId"
                           WHERE 1=1
                               AND bvcm."HebRefId" BETWEEN @StartRefId AND @EndRefId
                               AND NOT EXISTS (SELECT 1
                                               FROM "unshackled-word"."BibleVerseCountingMapping" m
                                               WHERE m."LxxRefId" = t."LxxRefId"
                                                   AND m."HebRefId" < t."SourceHebRefId")
                           ORDER BY bvcm."HebRefId", t."Id"
                           LIMIT @Limit
                           """;

        var rows = await _dbReader.ReadAsListAsync<TskCrossReference>(sql, new { StartRefId = startHebRefId, EndRefId = endHebRefId, Limit = limit });
        return rows.ToList();
    }
}
