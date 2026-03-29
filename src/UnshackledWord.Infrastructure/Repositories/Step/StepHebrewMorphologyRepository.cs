using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Abstractions.Step;
using UnshackledWord.Domain.Models.Dbo.Step;
using UnshackledWord.Domain.Extensions;

namespace UnshackledWord.Infrastructure.Repositories.Step;

public sealed class StepHebrewMorphologyRepository : IStepHebrewMorphologyRepository
{
    private readonly IDbWriter _dbWriter;
    private readonly IDbReader _dbReader;

    public StepHebrewMorphologyRepository(IDbWriter dbWriter, IDbReader dbReader)
    {
        _dbWriter = dbWriter;
        _dbReader = dbReader;
    }

    public async Task<int> CountByFilterAsync(StepHebrewMorphologyFilter filter, CancellationToken token = default)
    {
        var sql = $"""
                   SELECT COUNT(*)
                   FROM {StepHebrewMorphologyDbo.DbName} AS s
                   WHERE 1=1
                     {(filter.PartOfSpeech.IsNullOrEmpty() ? string.Empty : $"AND s.\"{nameof(StepHebrewMorphologyDbo.PartOfSpeech)}\" = @PartOfSpeech")};
                   """;

        return await _dbReader.ExecuteScalarAsync<int>(sql, filter);
    }

    public async Task BulkInsertAsync(StepHebrewMorphologyDbo[] entries, CancellationToken token = default)
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
            Form = new List<string?>(dataSize),
            Tense = new List<string?>(dataSize),
            Mood = new List<string?>(dataSize),
            Person = new List<string?>(dataSize),
            Number = new List<string?>(dataSize),
            Gender = new List<string?>(dataSize),
            State = new List<string?>(dataSize),
        };

        foreach (var entry in entries)
        {
            parameters.Id.Add(entry.Id);
            parameters.Code.Add(entry.Code);
            parameters.PartOfSpeech.Add(entry.PartOfSpeech);
            parameters.Form.Add(entry.Form);
            parameters.Tense.Add(entry.Tense);
            parameters.Mood.Add(entry.Mood);
            parameters.Person.Add(entry.Person);
            parameters.Number.Add(entry.Number);
            parameters.Gender.Add(entry.Gender);
            parameters.State.Add(entry.State);
        }

        var names = PropertyListHelper.GetPropertyNames(parameters);

        var sql = $"""
                   INSERT INTO {StepHebrewMorphologyDbo.DbName} (
                       {names.Select(x => $"\"{x}\"").JoinStrings(",")}
                   )
                   SELECT *
                   FROM UNNEST({names.Select(x => $"@{x}").JoinStrings(",")})
                   ON CONFLICT DO NOTHING;
                   """;

        await _dbWriter.WriteAsync(sql, parameters);
    }
}
