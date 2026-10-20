using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Repositories;
using UnshackledWord.Domain.Models.Study;

namespace UnshackledWord.Infrastructure.Repositories;

public sealed class StepBibleVerseRepository : IStepBibleVerseRepository
{
    private readonly IDbReader _dbReader;

    public StepBibleVerseRepository(IDbReader dbReader)
    {
        _dbReader = dbReader;
    }

    public async Task<IReadOnlyList<StepBibleVerse>> GetByHebRefIdRangeAsync(int startRefId, int endRefId, int limit, CancellationToken token = default)
    {
        const string sql = """
                           SELECT sbv."HebRefId"
                                , sbv."VerseText"
                                , sbv."LemmaText"
                           FROM "unshackled-word"."StepBibleVerses" sbv
                           WHERE 1=1
                               AND sbv."HebRefId" BETWEEN @StartRefId AND @EndRefId
                           ORDER BY sbv."HebRefId"
                           LIMIT @Limit
                           """;

        var rows = await _dbReader.ReadAsListAsync<StepBibleVerse>(sql, new { StartRefId = startRefId, EndRefId = endRefId, Limit = limit });
        return rows.ToList();
    }
}
