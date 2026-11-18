using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Repositories;
using UnshackledWord.Domain.Models.BibleStructure.References;

namespace UnshackledWord.Infrastructure.Repositories;

public sealed class VerseCountingRepository : IVerseCountingRepository
{
    private readonly IDbReader _dbReader;

    public VerseCountingRepository(IDbReader dbReader)
    {
        _dbReader = dbReader;
    }

    public async Task<IReadOnlyList<VerseCountingRow>> GetDifferingAsync(CancellationToken token = default)
    {
        const string sql = """
                           SELECT "LxxRefId"
                                , "HebRefId"
                           FROM "unshackled-word"."BibleVerseCountingMapping"
                           WHERE "LxxRefId" <> "HebRefId"
                           ORDER BY "LxxRefId", "HebRefId"
                           """;

        var rows = await _dbReader.ReadAsListAsync<VerseCountingRow>(sql);
        return rows.ToList();
    }
}
