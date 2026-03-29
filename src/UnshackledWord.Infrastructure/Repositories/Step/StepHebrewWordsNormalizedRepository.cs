using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Abstractions.Step;
using UnshackledWord.Domain.Extensions;
using UnshackledWord.Domain.Models.Dbo.Step;

namespace UnshackledWord.Infrastructure.Repositories.Step;

public sealed class StepHebrewWordsNormalizedRepository : IStepHebrewWordsNormalizedRepository
{
    private readonly IDbWriter _dbWriter;
    private readonly IDbReader _dbReader;

    public StepHebrewWordsNormalizedRepository(IDbWriter dbWriter, IDbReader dbReader)
    {
        _dbWriter = dbWriter;
        _dbReader = dbReader;
    }

    public async Task<int> CountByFilterAsync(StepNormalizedHebrewWordsFilter filter, CancellationToken token = default)
    {
        var sql = $"""
                   SELECT COUNT(*)
                   FROM {StepHebrewWordsNormalizedDbo.DbName} AS s
                   WHERE 1=1
                   """;

        return await _dbReader.ExecuteScalarAsync<int>(sql, filter);
    }

    public async Task BulkInsertAsync(StepHebrewWordsNormalizedDbo[] entries, CancellationToken token = default)
    {
        if (entries.Length == 0)
        {
            return;
        }

        var dataSize = entries.Length + 1;
        var parameters = new
        {
            Id = new List<int>(dataSize),
            IsRoot = new List<bool>(dataSize),
            Grammar = new List<string?>(dataSize),
            SuffixCode = new List<string?>(dataSize),
            Hebrew = new List<string>(dataSize),
            StrongsNumber = new List<string>(dataSize),
        };

        foreach (var entry in entries)
        {
            parameters.Id.Add(entry.Id);
            parameters.IsRoot.Add(entry.IsRoot);
            parameters.Grammar.Add(entry.Grammar);
            parameters.SuffixCode.Add(entry.SuffixCode);
            parameters.Hebrew.Add(entry.Hebrew);
            parameters.StrongsNumber.Add(entry.StrongsNumber);
        }

        var names = PropertyListHelper.GetPropertyNames(parameters);

        var sql = $"""
                   INSERT INTO {StepHebrewWordsNormalizedDbo.DbName} (
                       {names.Select(x => $"\"{x}\"").JoinStrings(",")}
                   )
                   SELECT *
                   FROM UNNEST({names.Select(x => $"@{x}").JoinStrings(",")})
                   ON CONFLICT DO NOTHING;
                   """;

        await _dbWriter.WriteAsync(sql, parameters);
    }

    public async Task BulkInsertAsync(StepHebrewWordsNormalizedToHebrewWordDbo[] entries, CancellationToken token = default)
    {
        if (entries.Length == 0)
        {
            return;
        }

        var dataSize = entries.Length + 1;
        var parameters = new
        {
            StepHebrewWordsId = new List<int>(dataSize),
            StepHebrewWordsNormalizedId = new List<int>(dataSize),
            PositionInWord = new List<int>(dataSize),
        };

        foreach (var entry in entries)
        {
            parameters.StepHebrewWordsId.Add(entry.StepHebrewWordsId);
            parameters.StepHebrewWordsNormalizedId.Add(entry.StepHebrewWordsNormalizedId);
            parameters.PositionInWord.Add(entry.PositionInWord);
        }
        var names = PropertyListHelper.GetPropertyNames(parameters);

        var sql = $"""
                   INSERT INTO {StepHebrewWordsNormalizedToHebrewWordDbo.DbName} (
                       {names.Select(x => $"\"{x}\"").JoinStrings(",")}
                   )
                   SELECT *
                   FROM UNNEST({names.Select(x => $"@{x}").JoinStrings(",")})
                   ON CONFLICT DO NOTHING;
                   """;

        await _dbWriter.WriteAsync(sql, parameters);
    }
}
