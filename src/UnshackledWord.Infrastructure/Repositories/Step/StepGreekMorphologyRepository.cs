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

        var dataSize = entries.Length + 1;
        var parameters = new
        {
            Id = new List<int>(dataSize),
            Code = new List<string>(dataSize),
            PartOfSpeech = new List<string>(dataSize),
            Voice = new List<string?>(dataSize),
            Tense = new List<string?>(dataSize),
            Mood = new List<string?>(dataSize),
            Person = new List<string?>(dataSize),
            Number = new List<string?>(dataSize),
            Gender = new List<string?>(dataSize),
            Degree = new List<string?>(dataSize),
            Extras = new List<string?>(dataSize),
            NameType = new List<string?>(dataSize),
        };

        foreach (var entry in entries)
        {
            parameters.Id.Add(entry.Id);
            parameters.Code.Add(entry.Code);
            parameters.PartOfSpeech.Add(entry.PartOfSpeech);
            parameters.Voice.Add(entry.Voice);
            parameters.Tense.Add(entry.Tense);
            parameters.Mood.Add(entry.Mood);
            parameters.Person.Add(entry.Person);
            parameters.Number.Add(entry.Number);
            parameters.Gender.Add(entry.Gender);
            parameters.Degree.Add(entry.Degree);
            parameters.Extras.Add(entry.Extras);
            parameters.NameType.Add(entry.NameType);
        }

        var names = PropertyListHelper.GetPropertyNames(parameters);

        var sql = $"""
                   INSERT INTO {StepGreekMorphologyDbo.DbName} (
                       {names.Select(x => $"\"{x}\"").JoinStrings(",")}
                   )
                   SELECT *
                   FROM UNNEST({names.Select(x => $"@{x}").JoinStrings(",")})
                   ON CONFLICT DO NOTHING;
                   """;

        await _dbWriter.WriteAsync(sql, parameters);
    }
}
