using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Features.Backup;
using UnshackledWord.Domain.Models.BibleStructure;

namespace UnshackledWord.Infrastructure.Repositories;

public class ElbDashboardRepository : IElbDashboardRepository
{
    private readonly IDbReader _dbReader;

    public ElbDashboardRepository(IDbReader dbReader)
    {
        _dbReader = dbReader;
    }

    public async Task<Dictionary<int, List<ElbMappingBackup>>> CreateBackupAsync(CancellationToken ct = default)
    {
        var sql = $"""
                   SELECT
                         ehm."HebRefId"
                       , ehm."PositionInVerse"
                       , ehm."GermanWordPart"
                       , ew."PlainWord" "ElbWord"
                       , shw."Hebrew" "StepWord"
                       , shw."PositionInVerse" "StepPositionInVerse"
                       , ehm."IsAddedWord"
                       , ew2."PositionInVerse" "ParentPositionInVerse"
                       , ew2."PlainWord" "ParentWord"
                   FROM "unshackled-word"."Elb1871HebrewMapping"     ehm
                       LEFT JOIN "unshackled-word"."Elb1871Words"    ew  ON ehm."ElbWordId" = ew."Id"
                       LEFT JOIN "unshackled-word"."StepHebrewWords" shw ON ehm."StepWordId" = shw."Id"
                       LEFT JOIN "unshackled-word"."Elb1871Words"    ew2 ON ehm."ParentGermanWordId" = ew2."Id"
                   WHERE 1=1
                   ORDER BY
                       ehm."HebRefId" ASC,
                       ehm."PositionInVerse" ASC,
                       shw."PositionInVerse" ASC,
                       shw."Hebrew" ASC;

                   SELECT
                         egm."HebRefId"
                       , egm."PositionInVerse"
                       , egm."GermanWordPart"
                       , ew."PlainWord" "ElbWord"
                       , sgw."Greek" "StepWord"
                       , sgw."PositionInVerse" "StepPositionInVerse"
                       , egm."IsAddedWord"
                       , ew2."PositionInVerse" "ParentPositionInVerse"
                       , ew2."PlainWord" "ParentWord"
                   FROM "unshackled-word"."Elb1871GreekMapping"     egm
                       LEFT JOIN "unshackled-word"."Elb1871Words"   ew  ON egm."ElbWordId" = ew."Id"
                       LEFT JOIN "unshackled-word"."StepGreekWords" sgw ON egm."StepWordId" = sgw."Id"
                       LEFT JOIN "unshackled-word"."Elb1871Words"   ew2 ON egm."ParentGermanWordId" = ew2."Id"
                   WHERE 1=1
                   ORDER BY
                       egm."HebRefId" ASC,
                       egm."PositionInVerse" ASC,
                       sgw."PositionInVerse" ASC;
                   """;

        var records = await _dbReader.ReadMultipleAsListAsync<ElbMappingBackup>(sql, null, async (reader) =>
        {
            var hebrewMappings = await reader.ReadAsync<ElbMappingBackup>();
            var greekMappings = await reader.ReadAsync<ElbMappingBackup>();

            hebrewMappings.AddRange(greekMappings);

            return hebrewMappings;
        }, ct);

        return records.GroupBy(x => BibleReference.FromRefId(x.HebRefId).BookId)
            .ToDictionary(group => group.Key, group => group.ToList());
    }
}
