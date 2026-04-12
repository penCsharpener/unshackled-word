using UnshackledWord.Application.Abstractions;

namespace UnshackledWord.Tooling.SeedDb.Services.StepBible.StrongsToText;

public sealed class StepStrongsRepository
{
    private readonly IDbReader _reader;

    public StepStrongsRepository(IDbReader reader)
    {
        _reader = reader;
    }

    public async Task<List<StrongsIdLangDto>> GetOriginalStrongs()
    {
        var sql = """
                  SELECT *
                  FROM (
                      SELECT sgw."Id", sgw."DisambiguatedStrongs", 2 "Language"
                      FROM "unshackled-word"."StepGreekWords" sgw
                      UNION
                      SELECT shw."Id", shw."DisambiguatedStrongs", 0 "Language"
                      FROM "unshackled-word"."StepHebrewWords" shw
                  ) "allStrongs"
                  """;

        return (await _reader.ReadAsListAsync<StrongsIdLangDto>(sql)).ToList();
    }
}