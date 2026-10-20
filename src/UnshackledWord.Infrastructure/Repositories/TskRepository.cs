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
        // Tsk uses LxxRefId numbering; translate source and targets via BibleVerseCountingMapping.
        // Unmapped targets fall back to the LxxRefId (identical numbering in most verses).
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
                           ORDER BY bvcm."HebRefId", t."Id"
                           LIMIT @Limit
                           """;

        var rows = await _dbReader.ReadAsListAsync<TskCrossReference>(sql, new { StartRefId = startHebRefId, EndRefId = endHebRefId, Limit = limit });
        return rows.ToList();
    }
}
