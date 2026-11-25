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
}
