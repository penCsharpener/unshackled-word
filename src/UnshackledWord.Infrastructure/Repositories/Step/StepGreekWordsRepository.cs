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

    public async Task<int> CountByFilterAsync(StepGreekWordFilter filter, CancellationToken token = default)
    {
        var sql = $"""
                   SELECT COUNT(*)
                   FROM {StepGreekWordDbo.DbName} AS w
                   WHERE 1=1
                     {(filter.IncludedBibleBookIds.IsNullOrEmpty() ? string.Empty : $"AND w.\"{nameof(IBibleWordOrderColumns.BibleBookId)}\" = ANY(@IncludedBibleBookIds)")}
                     {(filter.IncludeChapters.IsNullOrEmpty() ? string.Empty : $"AND w.\"{nameof(IBibleWordOrderColumns.Chapter)}\" = ANY(@IncludeChapters)")};
                   """;

        return await _dbReader.ExecuteScalarAsync<int>(sql, filter);
    }

    public async Task<IEnumerable<StepGreekWordDbo>> GetByFilterAsync(StepGreekWordFilter filter, CancellationToken token = default)
    {
        var sql = $"""
                   SELECT {filter.GetSelectColumns()}
                   FROM {StepGreekWordDbo.DbName} AS w
                   WHERE 1=1
                     {(filter.IncludedBibleBookIds.IsNullOrEmpty() ? string.Empty : $"AND w.\"{nameof(IBibleWordOrderColumns.BibleBookId)}\" = ANY(@IncludedBibleBookIds)")}
                     {(filter.IncludeChapters.IsNullOrEmpty() ? string.Empty : $"AND w.\"{nameof(IBibleWordOrderColumns.Chapter)}\" = ANY(@IncludeChapters)")};
                   """;

        return await _dbReader.ReadAsListAsync<StepGreekWordDbo>(sql, filter);
    }

    public async Task BulkInsertAsync(StepGreekWordDbo[] entries, CancellationToken token = default)
    {
        if (entries.Length == 0)
        {
            return;
        }

        var count = await CountByFilterAsync(new(), token);

        if (count > 0)
        {
            return;
        }

        await BulkInsertInternalNewAsync(entries, token);
    }

    private async Task BulkInsertInternalOldAsync(StepGreekWordDbo[] entries, CancellationToken token = default)
    {
        var valueList = new ColumnInsertCollection();

        foreach (var entry in entries)
        {
            valueList.AddInt(entry.Id);
            valueList.AddInt(entry.BibleBookId);
            valueList.AddInt(entry.Chapter);
            valueList.AddInt(entry.Verse);
            valueList.AddInt(entry.LxxRefId);
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

    private async Task BulkInsertInternalNewAsync(StepGreekWordDbo[] entries, CancellationToken token = default)
    {
        const int dataSize = 10001;
        var parameter = new
        {
            Id = new List<int>(dataSize),
            BibleBookId = new List<int>(dataSize),
            Chapter = new List<int>(dataSize),
            Verse = new List<int>(dataSize),
            LxxRefId = new List<int>(dataSize),
            PositionInVerse = new List<int>(dataSize),
            AltChapter = new List<int?>(dataSize),
            AltVerse = new List<int?>(dataSize),
            Type = new List<string>(dataSize),
            IsInNestleAland = new List<bool>(dataSize),
            IsInTextusReceptus = new List<bool>(dataSize),
            IsInOther = new List<bool>(dataSize),
            Greek = new List<string>(dataSize),
            GreekNoDiacritics = new List<string>(dataSize),
            Transliteration = new List<string>(dataSize),
            English = new List<string>(dataSize),
            Spanish = new List<string?>(dataSize),
            DisambiguatedStrongs = new List<string>(dataSize),
            Morphology = new List<string>(dataSize),
            Lemma = new List<string>(dataSize),
            LemmaNoDiacritics = new List<string>(dataSize),
            Gloss = new List<string>(dataSize),
            Editions = new List<string>(dataSize),
            MeaningVariants = new List<string?>(dataSize),
            SpellingVariants = new List<string?>(dataSize),
            SubMeaning = new List<string?>(dataSize),
            ConjoinWord = new List<string?>(dataSize),
            StrongInstance = new List<string?>(dataSize),
            AltStrongs = new List<string?>(dataSize)
        };

        foreach (var entry in entries)
        {
            parameter.Id.Add(entry.Id);
            parameter.BibleBookId.Add(entry.BibleBookId);
            parameter.Chapter.Add(entry.Chapter);
            parameter.Verse.Add(entry.Verse);
            parameter.LxxRefId.Add(entry.LxxRefId);
            parameter.PositionInVerse.Add(entry.PositionInVerse);
            parameter.AltChapter.Add(entry.AltChapter);
            parameter.AltVerse.Add(entry.AltVerse);
            parameter.Type.Add(entry.Type);
            parameter.IsInNestleAland.Add(entry.IsInNestleAland);
            parameter.IsInTextusReceptus.Add(entry.IsInTextusReceptus);
            parameter.IsInOther.Add(entry.IsInOther);
            parameter.Greek.Add(entry.Greek);
            parameter.GreekNoDiacritics.Add(entry.GreekNoDiacritics);
            parameter.Transliteration.Add(entry.Transliteration);
            parameter.English.Add(entry.English);
            parameter.Spanish.Add(entry.Spanish);
            parameter.DisambiguatedStrongs.Add(entry.DisambiguatedStrongs);
            parameter.Morphology.Add(entry.Morphology);
            parameter.Lemma.Add(entry.Lemma);
            parameter.LemmaNoDiacritics.Add(entry.LemmaNoDiacritics);
            parameter.Gloss.Add(entry.Gloss);
            parameter.Editions.Add(entry.Editions);
            parameter.MeaningVariants.Add(entry.MeaningVariants);
            parameter.SpellingVariants.Add(entry.SpellingVariants);
            parameter.SubMeaning.Add(entry.SubMeaning);
            parameter.ConjoinWord.Add(entry.ConjoinWord);
            parameter.StrongInstance.Add(entry.StrongInstance);
            parameter.AltStrongs.Add(entry.AltStrongs);
        }

        var sql = $"""
                   INSERT INTO {StepGreekWordDbo.DbName} (
                       "Id","BibleBookId","Chapter","Verse","LxxRefId","PositionInVerse","AltChapter","AltVerse","Type","IsInNestleAland","IsInTextusReceptus","IsInOther","Greek","GreekNoDiacritics","Transliteration","English","Spanish","DisambiguatedStrongs","Morphology","Lemma","LemmaNoDiacritics","Gloss","Editions","MeaningVariants","SpellingVariants","SubMeaning","ConjoinWord","StrongInstance","AltStrongs"
                   )
                   SELECT *
                   FROM UNNEST(@Id,@BibleBookId,@Chapter,@Verse,@LxxRefId,@PositionInVerse,@AltChapter,@AltVerse,@Type,@IsInNestleAland,@IsInTextusReceptus,@IsInOther,@Greek,@GreekNoDiacritics,@Transliteration,@English,@Spanish,@DisambiguatedStrongs,@Morphology,@Lemma,@LemmaNoDiacritics,@Gloss,@Editions,@MeaningVariants,@SpellingVariants,@SubMeaning,@ConjoinWord,@StrongInstance,@AltStrongs)
                   """;


        await _dbWriter.WriteAsync(sql, parameter);
    }
}
