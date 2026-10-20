using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Repositories;
using UnshackledWord.Domain.Models.Study;

namespace UnshackledWord.Infrastructure.Repositories;

public sealed class Elb1871VerseRepository : IElb1871VerseRepository
{
    private readonly IDbReader _dbReader;

    public Elb1871VerseRepository(IDbReader dbReader)
    {
        _dbReader = dbReader;
    }

    public async Task<IReadOnlyList<ElbVerse>> GetByHebRefIdRangeAsync(int startRefId, int endRefId, int limit, CancellationToken token = default)
    {
        const string sql = """
                           SELECT ev."HebRefId"
                                , ev."VerseText"
                           FROM "unshackled-word"."Elb1871Verses" ev
                           WHERE 1=1
                               AND ev."HebRefId" BETWEEN @StartRefId AND @EndRefId
                           ORDER BY ev."HebRefId"
                           LIMIT @Limit
                           """;

        var rows = await _dbReader.ReadAsListAsync<ElbVerse>(sql, new { StartRefId = startRefId, EndRefId = endRefId, Limit = limit });
        return rows.ToList();
    }
}
