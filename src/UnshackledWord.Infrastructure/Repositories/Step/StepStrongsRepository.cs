using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Abstractions.Step;
using UnshackledWord.Domain.Extensions;
using UnshackledWord.Domain.Models.Dbo.Step;

namespace UnshackledWord.Infrastructure.Repositories.Step;

public sealed class StepStrongsRepository : IStepStrongsRepository
{
    private readonly IDbWriter _dbWriter;
    private readonly IDbReader _dbReader;

    public StepStrongsRepository(IDbWriter dbWriter, IDbReader dbReader)
    {
        _dbWriter = dbWriter;
        _dbReader = dbReader;
    }

    public async Task<int> CountByFilterAsync(StepStrongsFilter filter, CancellationToken token = default)
    {
        var sql = $"""
                   SELECT COUNT(*)
                   FROM {StepStrongsDbo.DbName} AS s
                   WHERE 1=1
                     {(filter.IncludeExtendedStrongs.IsNullOrEmpty() ? string.Empty : $"AND s.\"{nameof(StepStrongsDbo.ExtendedStrongs)}\" = ANY(@IncludeExtendedStrongs)")};
                   """;

        return await _dbReader.ExecuteScalarAsync<int>(sql, filter);
    }

    public async Task<IEnumerable<StepStrongsDbo>> GetByFilterAsync(StepStrongsFilter filter, CancellationToken token = default)
    {
        var sql = $"""
                   SELECT {filter.GetSelectColumns()}
                   FROM {StepStrongsDbo.DbName} AS s
                   WHERE 1=1
                     {(filter.IncludeExtendedStrongs.IsNullOrEmpty() ? string.Empty : $"AND s.\"{nameof(StepStrongsDbo.ExtendedStrongs)}\" = ANY(@IncludeExtendedStrongs)")};
                   """;

        var parameter = new
        {
            filter.IncludeExtendedStrongs
        };

        return await _dbReader.ReadAsListAsync<StepStrongsDbo>(sql, parameter);
    }

    public async Task BulkInsertAsync(StepStrongsDbo[] entries, CancellationToken token = default)
    {
        if (entries.Length == 0)
        {
            return;
        }

        var valueList = new ColumnInsertCollection();

        foreach (var entry in entries)
        {
            valueList.AddInt(entry.Id);
            valueList.AddString(entry.ExtendedStrongs);
            valueList.AddString(entry.DisambiguatedStrongs);
            valueList.AddString(entry.UnifiedStrongs);
            valueList.AddString(entry.OriginalWord);
            valueList.AddString(entry.OriginalWordNoDiacritics);
            valueList.AddString(entry.Transliteration);
            valueList.AddString(entry.Morphology);
            valueList.AddString(entry.Gloss);
            valueList.AddString(entry.Lexicon);

            valueList.ValuesToInsertRow();
            valueList.Clear();
        }

        var sql = $"""
                   INSERT INTO {StepStrongsDbo.DbName} (
                       {valueList.GetColumnNames()}
                   ) VALUES
                   {valueList.GetAllInsertRows()}
                   """;

        var parameter = new { };

        await _dbWriter.WriteAsync(sql, parameter);
    }
}
