using Microsoft.Extensions.Logging;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Abstractions.Step;
using UnshackledWord.Domain.Extensions;
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
                   FROM {StepStrongsToTextDbo.DbName} AS w
                   WHERE 1=1
                   """;

        return await _dbReader.ExecuteScalarAsync<int>(sql);
    }

    public async Task BulkInsertInternalNewAsync(StepStrongsToTextDbo[] entries, CancellationToken token = default)
    {
        if (entries.Length == 0)
        {
            return;
        }

        var dataSize = entries.Length + 1;
        var parameters = new
        {
            Id = new List<int>(dataSize),
            LanguageId = new List<int>(dataSize),
            Number = new List<int>(dataSize),
            Extra = new List<string?>(dataSize),
            IsRoot = new List<bool>(dataSize),
            CoversNextWord = new List<bool>(dataSize),
            StepGreekWordId = new List<int?>(dataSize),
            StepHebrewWordId = new List<int?>(dataSize),
            Order = new List<int>(dataSize),
        };

        foreach (var entry in entries)
        {
            parameters.Id.Add(entry.Id);
            parameters.LanguageId.Add((int)entry.LanguageId);
            parameters.Number.Add(entry.Number);
            parameters.Extra.Add(entry.Extra);
            parameters.IsRoot.Add(entry.IsRoot);
            parameters.CoversNextWord.Add(entry.CoversNextWord);
            parameters.StepGreekWordId.Add(entry.StepGreekWordId);
            parameters.StepHebrewWordId.Add(entry.StepHebrewWordId);
            parameters.Order.Add(entry.Order);
        }

        var names = PropertyListHelper.GetPropertyNames(parameters);

        var sql = $"""
                   INSERT INTO {StepStrongsToTextDbo.DbName} (
                       {names.Select(x => $"\"{x}\"").JoinStrings(",")}
                   )
                   SELECT *
                   FROM UNNEST({names.Select(x => $"@{x}").JoinStrings(",")})
                   ON CONFLICT DO NOTHING;
                   """;

        await _dbWriter.WriteAsync(sql, parameters);
    }
}
