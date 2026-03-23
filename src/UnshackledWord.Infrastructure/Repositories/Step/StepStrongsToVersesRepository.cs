using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Abstractions.Step;
using UnshackledWord.Domain.Extensions;
using UnshackledWord.Domain.Models.Dbo.Step;

namespace UnshackledWord.Infrastructure.Repositories.Step;

public sealed class StepStrongsToVersesRepository : IStepStrongsToVersesRepository
{
    private readonly IDbWriter _dbWriter;
    private readonly IDbReader _dbReader;

    public StepStrongsToVersesRepository(IDbWriter dbWriter, IDbReader dbReader)
    {
        _dbWriter = dbWriter;
        _dbReader = dbReader;
    }

    public async Task<int> CountByFilterAsync(StepStrongsFilter filter, CancellationToken token = default)
    {
        var sql = $"""
                   SELECT COUNT(*)
                   FROM {StepStrongsToVersesDbo.DbName} AS s
                   WHERE 1=1
                     {(filter.IncludeExtendedStrongs.IsNullOrEmpty() ? string.Empty : $"AND s.\"{nameof(StepStrongsToVersesDbo.StrongsNumber)}\" = ANY(@IncludeExtendedStrongs)")};
                   """;

        return await _dbReader.ExecuteScalarAsync<int>(sql, filter);
    }

    public async Task BulkInsertAsync(StepStrongsToVersesDbo[] entries, CancellationToken token = default)
    {
        if (entries.Length == 0)
        {
            return;
        }

        var valueList = new ColumnInsertCollection();

        foreach (var entry in entries)
        {
            valueList.AddInt(entry.Id);
            valueList.AddInt(entry.BibleBookId);
            valueList.AddInt(entry.Chapter);
            valueList.AddInt(entry.Verse);
            valueList.AddBool(entry.IsRoot);
            valueList.AddString(entry.Grammar);
            valueList.AddString(entry.Hebrew);
            valueList.AddString(entry.Gloss);
            valueList.AddInt(entry.FirstOccuranceLxxRefId);
            valueList.AddInt(entry.LastOccuranceLxxRefId);
            valueList.AddString(entry.StrongsNumber);

            valueList.ValuesToInsertRow();
            valueList.Clear();
        }

        var sql = $"""
                   INSERT INTO {StepStrongsToVersesDbo.DbName} (
                       {valueList.GetColumnNames()}
                   ) VALUES
                   {valueList.GetAllInsertRows()}
                   ON CONFLICT DO NOTHING;
                   """;

        var parameter = new { };

        await _dbWriter.WriteAsync(sql, parameter);
    }
}
