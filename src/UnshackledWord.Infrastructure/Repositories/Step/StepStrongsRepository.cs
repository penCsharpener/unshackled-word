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
                   FROM {StepStrongsLexiconDbo.DbName} AS s
                   WHERE 1=1
                     {(filter.IncludeExtendedStrongs.IsNullOrEmpty() ? string.Empty : $"AND s.\"{nameof(StepStrongsLexiconDbo.Number)}\" = ANY(@IncludeExtendedStrongs)")};
                   """;

        return await _dbReader.ExecuteScalarAsync<int>(sql, filter);
    }

    public async Task<IEnumerable<StepStrongsLexiconDbo>> GetByFilterAsync(StepStrongsFilter filter, CancellationToken token = default)
    {
        var sql = $"""
                   SELECT {filter.GetSelectColumns()}
                   FROM {StepStrongsLexiconDbo.DbName} AS s
                   WHERE 1=1
                     {(filter.IncludeExtendedStrongs.IsNullOrEmpty() ? string.Empty : $"AND s.\"{nameof(StepStrongsLexiconDbo.Number)}\" = ANY(@IncludeExtendedStrongs)")};
                   """;

        var parameter = new
        {
            filter.IncludeExtendedStrongs
        };

        return await _dbReader.ReadAsListAsync<StepStrongsLexiconDbo>(sql, parameter);
    }

    public async Task BulkInsertAsync(StepStrongsLexiconDbo[] entries, CancellationToken token = default)
    {
        if (entries.Length == 0)
        {
            return;
        }

        var dataSize = entries.Length + 1;
        var parameters = new
        {
            Id = new List<int>(dataSize),
            LanguageId = new List<int>(dataSize),
            Number = new List<int>(dataSize),
            Extra = new List<string?>(dataSize),
            DisambiguatedExtra = new List<string?>(dataSize),
            OriginalWord = new List<string>(dataSize),
            OriginalWordNoDiacritics = new List<string>(dataSize),
            Transliteration = new List<string>(dataSize),
            Morphology = new List<string>(dataSize),
            Gloss = new List<string>(dataSize),
            Lexicon = new List<string?>(dataSize),
        };

        foreach (var entry in entries)
        {
            entry.UnifiedStrongs.ForEach(x => x.StepStrongsLexiconId = entry.Id);
            parameters.Id.Add(entry.Id);
            parameters.LanguageId.Add((int)entry.LanguageId);
            parameters.Number.Add(entry.Number);
            parameters.Extra.Add(entry.Extra);
            parameters.DisambiguatedExtra.Add(entry.DisambiguatedExtra);
            parameters.OriginalWord.Add(entry.OriginalWord);
            parameters.OriginalWordNoDiacritics.Add(entry.OriginalWordNoDiacritics);
            parameters.Transliteration.Add(entry.Transliteration);
            parameters.Morphology.Add(entry.Morphology);
            parameters.Gloss.Add(entry.Gloss);
            parameters.Lexicon.Add(entry.Lexicon);
        }

        var names = PropertyListHelper.GetPropertyNames(parameters);

        var sql = $"""
                   INSERT INTO {StepStrongsLexiconDbo.DbName} (
                       {names.Select(x => $"\"{x}\"").JoinStrings(",")}
                   )
                   SELECT *
                   FROM UNNEST({names.Select(x => $"@{x}").JoinStrings(",")})
                   ON CONFLICT DO NOTHING;
                   """;

        await _dbWriter.WriteAsync(sql, parameters);

        await BulkInsertAsync(entries.SelectMany(x => x.UnifiedStrongs).ToArray(), token);
    }

    public async Task BulkInsertAsync(StepUnifiedStrongsDbo[] entries, CancellationToken token = default)
    {
        if (entries.Length == 0)
        {
            return;
        }

        var dataSize = entries.Length + 1;
        var parameters = new
        {
            Id = new List<int>(dataSize),
            StepStrongsLexiconId = new List<int>(dataSize),
            LanguageId = new List<int>(dataSize),
            Number = new List<int>(dataSize),
            Extra = new List<string?>(dataSize),
        };

        foreach (var entry in entries)
        {
            parameters.Id.Add(entry.Id);
            parameters.StepStrongsLexiconId.Add(entry.StepStrongsLexiconId);
            parameters.LanguageId.Add((int)entry.LanguageId);
            parameters.Number.Add(entry.Number);
            parameters.Extra.Add(entry.Extra);
        }

        var names = PropertyListHelper.GetPropertyNames(parameters);

        var sql = $"""
                   INSERT INTO {StepUnifiedStrongsDbo.DbName} (
                       {names.Select(x => $"\"{x}\"").JoinStrings(",")}
                   )
                   SELECT *
                   FROM UNNEST({names.Select(x => $"@{x}").JoinStrings(",")})
                   ON CONFLICT DO NOTHING;
                   """;

        await _dbWriter.WriteAsync(sql, parameters);
    }
}
