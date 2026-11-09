using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Repositories;
using UnshackledWord.Domain.Models.Study;

namespace UnshackledWord.Infrastructure.Repositories;

public sealed class HebrewMorphemeRepository : IHebrewMorphemeRepository
{
    private readonly IDbReader _dbReader;

    public HebrewMorphemeRepository(IDbReader dbReader)
    {
        _dbReader = dbReader;
    }

    public async Task<IReadOnlyList<HebrewMorpheme>> GetByStepWordIdsAsync(int[] stepHebrewWordIds, CancellationToken token = default)
    {
        if (stepHebrewWordIds.Length == 0)
        {
            return [];
        }

        const string sql = """
                           SELECT shwn2h."StepHebrewWordsId"
                                , shwn2h."PositionInWord"
                                , shwn."IsRoot"
                                , shwn."Hebrew"
                                , shwn."StrongsNumber"
                                , shwn."Grammar"
                                , shwn."SuffixCode"
                                , shm."PartOfSpeech"
                                , shm."Stem"
                                , shm."Form"
                                , shm."Person"
                                , shm."Gender"
                                , shm."Number"
                                , shm."State"
                           FROM "unshackled-word"."StepHebrewWordsNormalizedToHebrewWords" shwn2h
                               INNER JOIN "unshackled-word"."StepHebrewWordsNormalized" shwn ON shwn2h."StepHebrewWordsNormalizedId" = shwn."Id"
                               LEFT JOIN "unshackled-word"."StepHebrewMorphology" shm ON shwn."Grammar" = shm."Code"
                           WHERE 1=1
                               AND shwn2h."StepHebrewWordsId" = ANY(@StepHebrewWordIds)
                           ORDER BY shwn2h."StepHebrewWordsId", shwn2h."PositionInWord"
                           """;

        var rows = await _dbReader.ReadAsListAsync<HebrewMorpheme>(sql, new { StepHebrewWordIds = stepHebrewWordIds });
        return rows.ToList();
    }
}
