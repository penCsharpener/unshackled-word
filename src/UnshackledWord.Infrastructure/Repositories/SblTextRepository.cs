using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Repositories;
using UnshackledWord.Domain.Models.Study;

namespace UnshackledWord.Infrastructure.Repositories;

public sealed class SblTextRepository : ISblTextRepository
{
    private readonly IDbReader _dbReader;

    public SblTextRepository(IDbReader dbReader)
    {
        _dbReader = dbReader;
    }

    public async Task<IReadOnlyList<SblVerse>> GetByRefIdRangeAsync(int startRefId, int endRefId, int limit, CancellationToken token = default)
    {
        const string sql = """
                           SELECT st."LxxRefId"
                                , st."VerseText"
                           FROM "unshackled-word"."SblText" st
                           WHERE 1=1
                               AND st."LxxRefId" BETWEEN @StartRefId AND @EndRefId
                           ORDER BY st."LxxRefId"
                           LIMIT @Limit
                           """;

        var rows = await _dbReader.ReadAsListAsync<SblVerse>(sql, new { StartRefId = startRefId, EndRefId = endRefId, Limit = limit });
        return rows.ToList();
    }
}
