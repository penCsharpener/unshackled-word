using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Repositories;
using UnshackledWord.Domain.Models.Study;

namespace UnshackledWord.Infrastructure.Repositories;

public sealed class GreekEditionRepository : IGreekEditionRepository
{
    private readonly IDbReader _dbReader;

    public GreekEditionRepository(IDbReader dbReader)
    {
        _dbReader = dbReader;
    }

    public async Task<IReadOnlyList<GreekEditionRow>> GetCatalogAsync(CancellationToken token = default)
    {
        const string sql = """
                           SELECT ge."Id"
                                , ge."Code"
                                , ge."Name"
                                , ge."Year"
                                , ge."Kind"
                                , ge."IsMain"
                                , ge."Sort"
                           FROM "unshackled-word"."GreekEditions" ge
                           WHERE 1=1
                           ORDER BY ge."Sort", ge."Code"
                           """;

        var rows = await _dbReader.ReadAsListAsync<GreekEditionRow>(sql);
        return rows.ToList();
    }

    public async Task<IReadOnlyList<ChapterWordEditionMarkRow>> GetChapterEditionMarksAsync(int bibleBookId, int chapter, CancellationToken token = default)
    {
        var rows = await _dbReader.ReadAsListAsync<ChapterWordEditionMarkRow>(ChapterEditionMarksSql, new { BibleBookId = bibleBookId, Chapter = chapter });
        return rows.ToList();
    }

    /// <summary>Public so the database test can run it.</summary>
    public const string ChapterEditionMarksSql = """
                                                 SELECT swe."StepWordId"
                                                      , COALESCE(array_agg(ge."Code" ORDER BY ge."Sort", ge."Code") FILTER (WHERE ge."IsMain"), ARRAY[]::text[]) "MainCodes"
                                                      , (COUNT(*) FILTER (WHERE ge."IsMain"))::int "MainCount"
                                                      , (SELECT COUNT(*) FROM "unshackled-word"."GreekEditions" m WHERE m."IsMain")::int "MainCatalogCount"
                                                      , COALESCE(bool_or((swe."Shift" IS NOT NULL AND swe."Shift" <> 0) OR swe."ShiftRaw" IS NOT NULL), FALSE) "HasShift"
                                                 FROM "unshackled-word"."StepGreekWordEditions" swe
                                                     JOIN "unshackled-word"."GreekEditions" ge ON ge."Id" = swe."EditionId"
                                                     JOIN "unshackled-word"."StepGreekWords" sgw ON sgw."Id" = swe."StepWordId"
                                                 WHERE 1=1
                                                     AND sgw."LxxRefId" IN (SELECT DISTINCT ew."HebRefId"
                                                                            FROM "unshackled-word"."Elb1871Words" ew
                                                                            WHERE 1=1
                                                                                AND ew."BibleBookId" = @BibleBookId
                                                                                AND ew."Chapter" = @Chapter)
                                                 GROUP BY swe."StepWordId"
                                                 HAVING COUNT(*) FILTER (WHERE ge."IsMain") < (SELECT COUNT(*) FROM "unshackled-word"."GreekEditions" m WHERE m."IsMain")
                                                     OR bool_or((swe."Shift" IS NOT NULL AND swe."Shift" <> 0) OR swe."ShiftRaw" IS NOT NULL)
                                                 """;
}
