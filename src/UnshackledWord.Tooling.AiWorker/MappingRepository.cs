using UnshackledWord.Application.Abstractions;
using UnshackledWord.Tooling.AiWorker.Models;

namespace UnshackledWord.Tooling.AiWorker;

public class MappingRepository
{
    private readonly IDbReader _dbReader;

    public MappingRepository(IDbReader dbReader)
    {
        _dbReader = dbReader;
    }

    internal async Task<IEnumerable<BibleReference>> GetGreekNtStructureAsync()
    {
        var sql = """
                  select ew."BibleBookId", ew."Chapter", ew."Verse"
                  from "unshackled-word"."Elb1871Words" ew
                  where ew."BibleBookId" >= 40
                  group by ew."BibleBookId", ew."Chapter", ew."Verse"
                  order by ew."BibleBookId", ew."Chapter", ew."Verse"
                  """;

        var structureData = await _dbReader.ReadAsListAsync<BibleReference>(sql);

        return structureData;
    }

    internal async Task<IEnumerable<ElbVerseData>> GetElbVerseDataAsync(int bookId, int chapter, int verse)
    {
        var sql = $"""
                  select ew."Id", ew."WordInContext", ew."PositionInVerse"
                  from "unshackled-word"."Elb1871Words" ew
                  where   ew."BibleBookId" = {bookId}
                      and ew."Chapter" = {chapter}
                      and ew."Verse" = {verse}
                  order by ew."PositionInVerse" asc
                  """;

        return await _dbReader.ReadAsListAsync<ElbVerseData>(sql);
    }

    internal async Task<IEnumerable<StepGreekVerseData>> GetStepGreekVerseDataAsync(int bookId, int chapter, int verse)
    {
        var sql = $"""
                   select sgw."Id", sgw."Greek", sgw."PositionInVerse", sgw."DisambiguatedStrongs"
                   from "unshackled-word"."StepGreekWords" sgw
                   where   sgw."BibleBookId" = {bookId}
                       and sgw."Chapter" = {chapter}
                       and sgw."Verse" = {verse}
                   order by sgw."PositionInVerse" asc
                   """;

        return await _dbReader.ReadAsListAsync<StepGreekVerseData>(sql);
    }
}
