using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Abstractions.Step;
using UnshackledWord.Domain.Models.Dbo.Step;
using UnshackledWord.Domain.Extensions;

namespace UnshackledWord.Infrastructure.Repositories.Step;

public sealed class StepHebrewMorphologyRepository : IStepHebrewMorphologyRepository
{
    private readonly IDbWriter _dbWriter;
    private readonly IDbReader _dbReader;

    public StepHebrewMorphologyRepository(IDbWriter dbWriter, IDbReader dbReader)
    {
        _dbWriter = dbWriter;
        _dbReader = dbReader;
    }

    public async Task<int> CountByFilterAsync(StepHebrewMorphologyFilter filter, CancellationToken token = default)
    {
        var sql = $"""
                   SELECT COUNT(*)
                   FROM {StepHebrewMorphologyDbo.DbName} AS s
                   WHERE 1=1
                     {(filter.PartOfSpeech.IsNullOrEmpty() ? string.Empty : $"AND s.\"{nameof(StepHebrewMorphologyDbo.PartOfSpeech)}\" = @PartOfSpeech")};
                   """;

        return await _dbReader.ExecuteScalarAsync<int>(sql, filter);
    }

    public async Task BulkInsertAsync(StepHebrewMorphologyDbo[] entries, CancellationToken token = default)
    {
        if (entries.Length == 0)
        {
            return;
        }

        var valueList = new ColumnInsertCollection();

        foreach (var entry in entries)
        {
            valueList.AddInt(entry.Id);

            valueList.AddString(entry.Code);
            valueList.AddString(entry.PartOfSpeech);
            valueList.AddString(entry.Form);
            valueList.AddString(entry.Tense);
            valueList.AddString(entry.Mood);
            valueList.AddString(entry.Person);
            valueList.AddString(entry.Number);
            valueList.AddString(entry.Gender);
            valueList.AddString(entry.State);

            valueList.ValuesToInsertRow();
            valueList.Clear();
        }

        var sql = $"""
                   INSERT INTO {StepHebrewMorphologyDbo.DbName} (
                       {valueList.GetColumnNames()}
                   ) VALUES
                   {valueList.GetAllInsertRows()}
                   """;

        var parameter = new { };

        await _dbWriter.WriteAsync(sql, parameter);
    }
}
