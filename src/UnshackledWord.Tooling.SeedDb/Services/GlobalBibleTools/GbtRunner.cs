using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Tooling.SeedDb.Services.Abstractions;

namespace UnshackledWord.Tooling.SeedDb.Services.GlobalBibleTools;

public class GbtRunner : IRunner
{
    private readonly GbtCsvStrategy _gbtCsvStrategy;
    private readonly IDbReader _dbReader;

    public GbtRunner(GbtCsvStrategy gbtCsvStrategy, IDbReader dbReader)
    {
        _gbtCsvStrategy = gbtCsvStrategy;
        _dbReader = dbReader;
    }

    public async Task Run(CancellationToken token = default)
    {
        var count = await GetCountAsync(token);
        if (count > 0)
        {
            return;
        }

        await _gbtCsvStrategy.SaveToDatabase(null, token);
    }

    private async Task<int> GetCountAsync(CancellationToken token = default)
    {
        var sql = """
                  select COUNT(*)
                  from "unshackled-word"."SourceWords"
                  """;

        return await _dbReader.ExecuteScalarAsync<int>(sql);
    }
}
