using Microsoft.Extensions.Logging;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Abstractions.Step;
using UnshackledWord.Domain.Models.Dbo.Step;

namespace UnshackledWord.Infrastructure.Repositories.Step;

public sealed class StepStrongsNumbersRepository : IStepStrongsNumbersRepository
{
    private readonly IDbWriter _dbWriter;
    private readonly IDbReader _dbReader;
    private readonly ILogger<StepStrongsNumbersRepository> _logger;

    public StepStrongsNumbersRepository(IDbWriter dbWriter, IDbReader dbReader, ILogger<StepStrongsNumbersRepository> logger)
    {
        _dbWriter = dbWriter;
        _dbReader = dbReader;
        _logger = logger;
    }

    public async Task<int> CountByFilterAsync(CancellationToken token = default)
    {
        var sql = $"""
                   SELECT COUNT(*)
                   FROM {StrongsNumberDbo.DbName} AS w
                   WHERE 1=1
                   """;

        return await _dbReader.ExecuteScalarAsync<int>(sql);
    }

    public async Task BulkInsertInternalNewAsync(StrongsNumberDbo[] entries, CancellationToken token = default)
    {
        var count = await CountByFilterAsync(token);

        if (count > 0)
        {
            return;
        }

        var dataSize = entries.Length + 1;
        var parameter = new
        {
            Id = new List<int>(dataSize),
            LanguageId = new List<int>(dataSize),
            Number = new List<int>(dataSize),
            Extra = new List<string?>(dataSize),
            StrongsType = new List<int>(dataSize),
            IsRoot = new List<bool>(dataSize),
            CoversNextWord = new List<bool>(dataSize),
            StepGreekWordId = new List<int?>(dataSize),
            StepHebrewWordId = new List<int?>(dataSize),
            Order = new List<int>(dataSize),
        };

        foreach (var entry in entries)
        {
            parameter.Id.Add(entry.Id);
            parameter.LanguageId.Add((int)entry.LanguageId);
            parameter.Number.Add(entry.Number);
            parameter.Extra.Add(entry.Extra);
            parameter.StrongsType.Add((int)entry.StrongsType);
            parameter.IsRoot.Add(entry.IsRoot);
            parameter.CoversNextWord.Add(entry.CoversNextWord);
            parameter.StepGreekWordId.Add(entry.StepGreekWordId);
            parameter.StepHebrewWordId.Add(entry.StepHebrewWordId);
            parameter.Order.Add(entry.Order);
        }

        var sql = $"""
                   INSERT INTO {StrongsNumberDbo.DbName} (
                       "Id","LanguageId","Number","Extra","StrongsType","IsRoot","CoversNextWord","StepGreekWordId","StepHebrewWordId","Order"
                   )
                   SELECT *
                   FROM UNNEST(@Id,@LanguageId,@Number,@Extra,@StrongsType,@IsRoot,@CoversNextWord,@StepGreekWordId,@StepHebrewWordId,@Order)
                   ON CONFLICT DO NOTHING;
                   """;

        await _dbWriter.WriteAsync(sql, parameter);
    }
}
