using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Abstractions.Step;
using UnshackledWord.Domain.Extensions;
using UnshackledWord.Domain.Models;
using UnshackledWord.Domain.Models.Dbo.Step;

namespace UnshackledWord.Infrastructure.Repositories.Step;

public sealed class StepGreekWordsRepository : IStepGreekWordsRepository
{
    private readonly IDbWriter _dbWriter;
    private readonly IDbReader _dbReader;

    public StepGreekWordsRepository(IDbWriter dbWriter, IDbReader dbReader)
    {
        _dbWriter = dbWriter;
        _dbReader = dbReader;
    }

    public async Task<List<StepGreekWordDbo>> GetByFilterAsync(StepGreekWordFilter filter, CancellationToken token = default)
    {
        return [];
    }

    public async Task<int> CountByFilterAsync(StepGreekWordFilter filter, CancellationToken token = default)
    {
        var sql = $"""
                   SELECT COUNT(*)
                   FROM {StepGreekWordDbo.DbName} AS w
                   WHERE 1=1
                     {(filter.IncludedBibleBookIds.IsNullOrEmpty() ? string.Empty : $"AND w.\"{nameof(IBibleWordOrderColumns.BibleBookId)}\" = ANY(@IncludedBibleBookIds)")}
                     {(filter.IncludeChapters.IsNullOrEmpty() ? string.Empty : $"AND w.\"{nameof(IBibleWordOrderColumns.Chapter)}\" = ANY(@IncludeChapters)")};
                   """;

        var parameter = new
        {
            filter.IncludedBibleBookIds,
            filter.IncludeChapters
        };

        return await _dbReader.ExecuteScalarAsync<int>(sql, parameter);
    }

    public async Task BulkInsertAsync(StepGreekWordDbo[] entries, CancellationToken token = default)
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
            valueList.AddBool(entry.IsInNestleAland);
            valueList.AddBool(entry.IsInTextusReceptus);
            valueList.AddBool(entry.IsInOther);
            valueList.AddString(entry.Greek);
            valueList.AddString(entry.GreekNoDiacritics);
            valueList.AddString(entry.Transliteration);
            valueList.AddString(entry.English);
            valueList.AddString(entry.Spanish);
            valueList.AddString(entry.DisambiguatedStrongs);
            valueList.AddString(entry.Morphology);
            valueList.AddString(entry.Lemma);
            valueList.AddString(entry.LemmaNoDiacritics);
            valueList.AddString(entry.Gloss);
            valueList.AddString(entry.Editions);
            valueList.AddString(entry.MeaningVariants);
            valueList.AddString(entry.SpellingVariants);
            valueList.AddString(entry.SubMeaning);
            valueList.AddString(entry.ConjoinWord);
            valueList.AddString(entry.StrongInstance);
            valueList.AddString(entry.AltStrongs);

            valueList.ValuesToInsertRow();
            valueList.Clear();
        }

        var sql = $"""
                   INSERT INTO {StepGreekWordDbo.DbName} (
                       {valueList.GetColumnNames()}
                   ) VALUES
                   {valueList.GetAllInsertRows()}
                   """;

        var parameter = new { };

        await _dbWriter.WriteAsync(sql, parameter);
    }
}
