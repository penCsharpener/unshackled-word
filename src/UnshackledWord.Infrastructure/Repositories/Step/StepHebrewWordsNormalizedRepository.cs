using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Abstractions.Step;
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

        var valueList = new ColumnInsertCollection();

        foreach (var entry in entries)
        {
            valueList.AddInt(entry.Id);
            valueList.AddBool(entry.IsRoot);
            valueList.AddString(entry.Grammar);
            valueList.AddString(entry.SuffixCode);
            valueList.AddString(entry.Hebrew);
            valueList.AddString(entry.StrongsNumber);

            valueList.ValuesToInsertRow();
            valueList.Clear();
        }

        var sql = $"""
                   INSERT INTO {StepHebrewWordsNormalizedDbo.DbName} (
                       {valueList.GetColumnNames()}
                   ) VALUES
                   {valueList.GetAllInsertRows()}
                   ON CONFLICT DO NOTHING;
                   """;

        var parameter = new { };

        await _dbWriter.WriteAsync(sql, parameter);
    }

    public async Task BulkInsertAsync(StepHebrewWordsNormalizedToHebrewWordDbo[] entries, CancellationToken token = default)
    {
        if (entries.Length == 0)
        {
            return;
        }

        var valueList = new ColumnInsertCollection();

        foreach (var entry in entries)
        {
            valueList.AddInt(entry.StepHebrewWordsId);
            valueList.AddInt(entry.StepHebrewWordsNormalizedId);
            valueList.AddInt(entry.PositionInWord);

            valueList.ValuesToInsertRow();
            valueList.Clear();
        }

        var sql = $"""
                   INSERT INTO {StepHebrewWordsNormalizedToHebrewWordDbo.DbName} (
                       {valueList.GetColumnNames()}
                   ) VALUES
                   {valueList.GetAllInsertRows()}
                   ON CONFLICT DO NOTHING;
                   """;

        var parameter = new { };

        await _dbWriter.WriteAsync(sql, parameter);
    }
}
