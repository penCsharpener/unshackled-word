using Dapper;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Features.Lxx;

namespace UnshackledWord.Infrastructure.Lxx;

/// <summary>Dapper reads of <c>LxxVerses</c>/<c>LxxWords</c> for the reader (roadmap 0028.4): three result sets in one round trip.</summary>
public sealed class LxxReadRepository : ILxxReadRepository
{
    private readonly IDbConnectionFactory _factory;

    public LxxReadRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<LxxChapterRows> GetByHebRangeAsync(int fromHebRefId, int toHebRefId, CancellationToken token = default)
    {
        const string sql = """
                           SELECT v."HebRefId"
                           FROM "unshackled-word"."Elb1871Verses" v
                           WHERE 1=1
                               AND v."HebRefId" BETWEEN @FromHebRefId AND @ToHebRefId
                           ORDER BY v."HebRefId";

                           SELECT l."Id"
                                , l."BibleBookId"
                                , l."TextForm"
                                , l."Chapter"
                                , l."Verse"
                                , l."Subverse"
                                , l."SortOrder"
                                , l."HebRefId"
                                , l."AfterHebRefId"
                                , l."MapKind"
                           FROM "unshackled-word"."LxxVerses" l
                           WHERE 1=1
                               AND (l."HebRefId" BETWEEN @FromHebRefId AND @ToHebRefId
                                    OR l."AfterHebRefId" BETWEEN @FromHebRefId AND @ToHebRefId)
                           ORDER BY l."SortOrder";

                           SELECT w."Id"
                                , w."LxxVerseId"
                                , w."Position"
                                , w."Greek"
                           FROM "unshackled-word"."LxxVerses" l
                           JOIN "unshackled-word"."LxxWords" w ON w."LxxVerseId" = l."Id"
                           WHERE 1=1
                               AND (l."HebRefId" BETWEEN @FromHebRefId AND @ToHebRefId
                                    OR l."AfterHebRefId" BETWEEN @FromHebRefId AND @ToHebRefId)
                           ORDER BY l."SortOrder", w."Position"
                           """;

        using var connection = await _factory.CreateDbConnectionAsync(token);
        using var grid = await connection.QueryMultipleAsync(new CommandDefinition(sql, new { FromHebRefId = fromHebRefId, ToHebRefId = toHebRefId }, cancellationToken: token));
        return new LxxChapterRows
        {
            ElbHebRefIds = (await grid.ReadAsync<int>()).ToList(),
            Units = (await grid.ReadAsync<LxxUnitRow>()).ToList(),
            Words = (await grid.ReadAsync<LxxWordRow>()).ToList(),
        };
    }
}
