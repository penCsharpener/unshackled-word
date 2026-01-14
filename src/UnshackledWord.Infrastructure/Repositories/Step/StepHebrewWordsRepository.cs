using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Abstractions.Step;
using UnshackledWord.Domain.Extensions;
using UnshackledWord.Domain.Models;
using UnshackledWord.Domain.Models.Dbo.Step;

namespace UnshackledWord.Infrastructure.Repositories.Step;

public sealed class StepHebrewWordsRepository : IStepHebrewWordsRepository
{
    private readonly IDbWriter _dbWriter;
    private readonly IDbReader _dbReader;

    public StepHebrewWordsRepository(IDbWriter dbWriter, IDbReader dbReader)
    {
        _dbWriter = dbWriter;
        _dbReader = dbReader;
    }

    public async Task<int> CountByFilterAsync(StepHebrewWordFilter filter, CancellationToken token = default)
    {
        var sql = $"""
                   SELECT COUNT(*)
                   FROM {StepHebrewWordDbo.DbName} AS w
                   WHERE 1=1
                     {(filter.IncludedBibleBookIds.IsNullOrEmpty() ? string.Empty : $"AND w.\"{nameof(IBibleWordOrderColumns.BibleBookId)}\" = ANY(@IncludedBibleBookIds)")}
                     {(filter.IncludeChapters.IsNullOrEmpty() ? string.Empty : $"AND w.\"{nameof(IBibleWordOrderColumns.Chapter)}\" = ANY(@IncludeChapters)")};
                   """;

        return await _dbReader.ExecuteScalarAsync<int>(sql, filter);
    }

    public async Task<IEnumerable<StepHebrewWordDbo>> GetByFilterAsync(StepHebrewWordFilter filter, CancellationToken token = default)
    {
        var sql = $"""
                   SELECT {filter.GetSelectColumns()}
                   FROM {StepHebrewWordDbo.DbName} AS w
                   WHERE 1=1
                     {(filter.IncludedBibleBookIds.IsNullOrEmpty() ? string.Empty : $"AND w.\"{nameof(IBibleWordOrderColumns.BibleBookId)}\" = ANY(@IncludedBibleBookIds)")}
                     {(filter.IncludeChapters.IsNullOrEmpty() ? string.Empty : $"AND w.\"{nameof(IBibleWordOrderColumns.Chapter)}\" = ANY(@IncludeChapters)")};
                   """;

        return await _dbReader.ReadAsListAsync<StepHebrewWordDbo>(sql, filter);
    }

    public async Task BulkInsertAsync(StepHebrewWordDbo[] entries, CancellationToken token = default)
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
            valueList.AddInt(entry.PositionInVerse);
            valueList.AddInt(entry.AltChapter);
            valueList.AddInt(entry.AltVerse);
            valueList.AddString(entry.Type);
            valueList.AddString(entry.HebrewNormalised);
            valueList.AddString(entry.Hebrew);
            valueList.AddString(entry.HebrewNoDiacritics);
            valueList.AddString(entry.Transliteration);
            valueList.AddString(entry.Gloss);
            valueList.AddString(entry.DisambiguatedStrongs);
            valueList.AddString(entry.Grammar);
            valueList.AddString(entry.MeaningVariants);
            valueList.AddString(entry.SpellingVariants);
            valueList.AddString(entry.RootDisambiguatedStrongsInstance);
            valueList.AddString(entry.AlternativeStrongs);
            valueList.AddString(entry.ConjoinWord);
            valueList.AddString(entry.ExpandedStrongTags);

            valueList.ValuesToInsertRow();
            valueList.Clear();
        }

        var sql = $"""
                   INSERT INTO {StepHebrewWordDbo.DbName} (
                       {valueList.GetColumnNames()}
                   ) VALUES
                   {valueList.GetAllInsertRows()}
                   """;

        var parameter = new { };

        await _dbWriter.WriteAsync(sql, parameter);
    }
}
