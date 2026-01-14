using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Abstractions.Step;
using UnshackledWord.Domain.Extensions;
using UnshackledWord.Domain.Models.Dbo.Step;

namespace UnshackledWord.Infrastructure.Repositories.Step;

public sealed class StepGreekMorphologyRepository : IStepGreekMorphologyRepository
{
    private readonly IDbWriter _dbWriter;
    private readonly IDbReader _dbReader;

    public StepGreekMorphologyRepository(IDbWriter dbWriter, IDbReader dbReader)
    {
        _dbWriter = dbWriter;
        _dbReader = dbReader;
    }

    public async Task<int> CountByFilterAsync(StepGreekMorphologyFilter filter, CancellationToken token = default)
    {
        var sql = $"""
                   SELECT COUNT(*)
                   FROM {StepGreekMorphologyDbo.DbName} AS s
                   WHERE 1=1
                     {(filter.PartOfSpeech.IsNullOrEmpty() ? string.Empty : $"AND s.\"{nameof(StepGreekMorphologyDbo.PartOfSpeech)}\" = @PartOfSpeech")};
                   """;

        return await _dbReader.ExecuteScalarAsync<int>(sql, filter);
    }

    public async Task BulkInsertAsync(StepGreekMorphologyDbo[] entries, CancellationToken token = default)
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
            valueList.AddString(entry.Voice);
            valueList.AddString(entry.Tense);
            valueList.AddString(entry.Mood);
            valueList.AddString(entry.Person);
            valueList.AddString(entry.Number);
            valueList.AddString(entry.Gender);
            valueList.AddString(entry.Degree);
            valueList.AddString(entry.Extras);
            valueList.AddString(entry.NameType);

            valueList.ValuesToInsertRow();
            valueList.Clear();
        }

        var sql = $"""
                   INSERT INTO {StepGreekMorphologyDbo.DbName} (
                       {valueList.GetColumnNames()}
                   ) VALUES
                   {valueList.GetAllInsertRows()}
                   """;

        var parameter = new { };

        await _dbWriter.WriteAsync(sql, parameter);
    }
}
