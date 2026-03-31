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
                   """;

        return await _dbReader.ExecuteScalarAsync<int>(sql, filter);
    }

    public async Task<IEnumerable<StepHebrewWordDbo>> GetByFilterAsync(StepHebrewWordFilter filter, CancellationToken token = default)
    {
        var sql = $"""
                   SELECT {filter.GetSelectColumns()}
                   FROM {StepHebrewWordDbo.DbName} AS w
                   WHERE 1=1
                   ORDER BY w."LxxRefId", w."PositionInVerse";
                   """;

        return await _dbReader.ReadAsListAsync<StepHebrewWordDbo>(sql, filter);
    }

    public async Task BulkInsertAsync(StepHebrewWordDbo[] entries, CancellationToken token = default)
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

        var dataSize = entries.Length + 1;
        var parameters = new
        {
            Id = new List<int>(dataSize),
            LxxRefId = new List<int>(dataSize),
            PositionInVerse = new List<int>(dataSize),
            AltChapter = new List<int?>(dataSize),
            AltVerse = new List<int?>(dataSize),
            Type = new List<string>(dataSize),
            HebrewNormalised = new List<string>(dataSize),
            Hebrew = new List<string>(dataSize),
            HebrewNoDiacritics = new List<string>(dataSize),
            Transliteration = new List<string>(dataSize),
            Gloss = new List<string>(dataSize),
            DisambiguatedStrongs = new List<string>(dataSize),
            Grammar = new List<string>(dataSize),
            MeaningVariants = new List<string?>(dataSize),
            SpellingVariants = new List<string?>(dataSize),
            RootDisambiguatedStrongsInstance = new List<string?>(dataSize),
            AlternativeStrongs = new List<string?>(dataSize),
            ConjoinWord = new List<string?>(dataSize),
            ExpandedStrongTags = new List<string?>(dataSize),
        };

        foreach (var entry in entries)
        {
            parameters.Id.Add(entry.Id);
            parameters.LxxRefId.Add(entry.LxxRefId);
            parameters.PositionInVerse.Add(entry.PositionInVerse);
            parameters.AltChapter.Add(entry.AltChapter);
            parameters.AltVerse.Add(entry.AltVerse);
            parameters.Type.Add(entry.Type);
            parameters.HebrewNormalised.Add(entry.HebrewNormalised);
            parameters.Hebrew.Add(entry.Hebrew);
            parameters.HebrewNoDiacritics.Add(entry.HebrewNoDiacritics);
            parameters.Transliteration.Add(entry.Transliteration);
            parameters.Gloss.Add(entry.Gloss);
            parameters.DisambiguatedStrongs.Add(entry.DisambiguatedStrongs);
            parameters.Grammar.Add(entry.Grammar);
            parameters.MeaningVariants.Add(entry.MeaningVariants);
            parameters.SpellingVariants.Add(entry.SpellingVariants);
            parameters.RootDisambiguatedStrongsInstance.Add(entry.RootDisambiguatedStrongsInstance);
            parameters.AlternativeStrongs.Add(entry.AlternativeStrongs);
            parameters.ConjoinWord.Add(entry.ConjoinWord);
            parameters.ExpandedStrongTags.Add(entry.ExpandedStrongTags);
        }

        var names = PropertyListHelper.GetPropertyNames(parameters);

        var sql = $"""
                   INSERT INTO {StepHebrewWordDbo.DbName} (
                       {names.Select(x => $"\"{x}\"").JoinStrings(",")}
                   )
                   SELECT *
                   FROM UNNEST({names.Select(x => $"@{x}").JoinStrings(",")})
                   ON CONFLICT DO NOTHING;
                   """;

        await _dbWriter.WriteAsync(sql, parameters);
    }
}
