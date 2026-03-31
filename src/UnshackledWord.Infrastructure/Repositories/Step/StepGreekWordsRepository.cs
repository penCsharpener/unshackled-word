using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Abstractions.Step;
using UnshackledWord.Domain.Extensions;
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
        var parameters = new
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
            parameters.Id.Add(entry.Id);
            parameters.LxxRefId.Add(entry.LxxRefId);
            parameters.PositionInVerse.Add(entry.PositionInVerse);
            parameters.AltChapter.Add(entry.AltChapter);
            parameters.AltVerse.Add(entry.AltVerse);
            parameters.Type.Add(entry.Type);
            parameters.IsInNestleAland.Add(entry.IsInNestleAland);
            parameters.IsInTextusReceptus.Add(entry.IsInTextusReceptus);
            parameters.IsInOther.Add(entry.IsInOther);
            parameters.Greek.Add(entry.Greek);
            parameters.GreekNoDiacritics.Add(entry.GreekNoDiacritics);
            parameters.Transliteration.Add(entry.Transliteration);
            parameters.English.Add(entry.English);
            parameters.Spanish.Add(entry.Spanish);
            parameters.DisambiguatedStrongs.Add(entry.DisambiguatedStrongs);
            parameters.Morphology.Add(entry.Morphology);
            parameters.Lemma.Add(entry.Lemma);
            parameters.LemmaNoDiacritics.Add(entry.LemmaNoDiacritics);
            parameters.Gloss.Add(entry.Gloss);
            parameters.Editions.Add(entry.Editions);
            parameters.MeaningVariants.Add(entry.MeaningVariants);
            parameters.SpellingVariants.Add(entry.SpellingVariants);
            parameters.SubMeaning.Add(entry.SubMeaning);
            parameters.ConjoinWord.Add(entry.ConjoinWord);
            parameters.StrongInstance.Add(entry.StrongInstance);
            parameters.AltStrongs.Add(entry.AltStrongs);
        }

        var names = PropertyListHelper.GetPropertyNames(parameters);

        var sql = $"""
                   INSERT INTO {StepGreekWordDbo.DbName} (
                       {names.Select(x => $"\"{x}\"").JoinStrings(",")}
                   )
                   SELECT *
                   FROM UNNEST({names.Select(x => $"@{x}").JoinStrings(",")})
                   ON CONFLICT DO NOTHING;
                   """;

        await _dbWriter.WriteAsync(sql, parameters);
    }
}
