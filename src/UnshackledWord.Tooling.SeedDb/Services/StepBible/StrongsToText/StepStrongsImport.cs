using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Abstractions.Step;
using UnshackledWord.Domain.Models.Dbo.Step;
using UnshackledWord.Tooling.SeedDb.Services.Abstractions;

namespace UnshackledWord.Tooling.SeedDb.Services.StepBible.StrongsToText;

public class StepStrongsImport : IRunner
{
    private readonly IDbReader _reader;
    private readonly IStepStrongsNumbersRepository _stepStrongsNumbersRepository;
    private readonly ILogger<StepStrongsImport> _logger;

    public StepStrongsImport(IDbReader reader, IStepStrongsNumbersRepository stepStrongsNumbersRepository, ILogger<StepStrongsImport> logger)
    {
        _reader = reader;
        _stepStrongsNumbersRepository = stepStrongsNumbersRepository;
        _logger = logger;
    }

    public async Task Run(CancellationToken token = default)
    {
        var count = await GetCountAsync();
        if (count > 0)
        {
            _logger.LogInformation("StepStrongsImport already has rows in the database: {count}", count);
            return;
        }

        var strongs = await GetOriginalStrongs();

        var mappedStrongs = strongs.ToDbo().ToList();

        await _stepStrongsNumbersRepository.BulkInsertInternalNewAsync(mappedStrongs, token);
    }

    public async Task<List<StrongsIdLangDto>> GetOriginalStrongs()
    {
        var sql = """
                  SELECT *
                  FROM (
                      SELECT sgw."Id", sgw."DisambiguatedStrongs" "Strongs", 2 "Language"
                      FROM "unshackled-word"."StepGreekWords" sgw
                      UNION
                      SELECT shw."Id", shw."DisambiguatedStrongs" "Strongs", 0 "Language"
                      FROM "unshackled-word"."StepHebrewWords" shw
                  ) "allStrongs"
                  """;

        return (await _reader.ReadAsListAsync<StrongsIdLangDto>(sql)).ToList();
    }

    public async Task<int> GetCountAsync()
    {
        var sql = $"""
                   SELECT COUNT(*)
                   FROM {StepStrongsToTextDbo.DbName}
                   """;

        return await _reader.ExecuteScalarAsync<int>(sql);
    }
}
