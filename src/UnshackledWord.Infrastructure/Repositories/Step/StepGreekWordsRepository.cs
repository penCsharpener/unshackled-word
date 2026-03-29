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
                   """;

        return await _dbReader.ExecuteScalarAsync<int>(sql, filter);
    }

    public async Task<IEnumerable<StepGreekWordDbo>> GetByFilterAsync(StepGreekWordFilter filter, CancellationToken token = default)
    {
        var sql = $"""
                   SELECT {filter.GetSelectColumns()}
                   FROM {StepGreekWordDbo.DbName} AS w
                   WHERE 1=1
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

    private async Task BulkInsertInternalNewAsync(StepGreekWordDbo[] entries, CancellationToken token = default)
    {
        var dataSize = entries.Length + 1;
        var parameter = new
        {
            Id = new List<int>(dataSize),
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
                   ON CONFLICT DO NOTHING;
                   """;


        await _dbWriter.WriteAsync(sql, parameter);
    }
}
