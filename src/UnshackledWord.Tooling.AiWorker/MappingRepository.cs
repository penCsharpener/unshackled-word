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

    internal async Task<IEnumerable<BibleReference>> GetGreekNtStructureByVerseAsync()
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

    internal async Task<IEnumerable<BibleReference>> GetGreekNtStructureByChapterAsync()
    {
        var sql = """
                  select ew."BibleBookId", ew."Chapter", MAX(ew."Verse") Verse
                  from "unshackled-word"."Elb1871Words" ew
                  where ew."BibleBookId" >= 40
                  group by ew."BibleBookId", ew."Chapter"
                  order by ew."BibleBookId", ew."Chapter"
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

    internal async Task<IEnumerable<VerseDataList<ElbVerseData>>> GetElbVerseDataAsync(int bookId, int chapter, int startVerse, int endVerse)
    {
        var sql = $"""
                   select ew."Id", ew."BibleBookId", ew."Chapter", ew."Verse", ew."WordInContext" "Word", ew."PositionInVerse"
                   from "unshackled-word"."Elb1871Words" ew
                   where   ew."BibleBookId" = {bookId}
                       and ew."Chapter" = {chapter}
                       and ew."Verse" >= {startVerse}
                       and ew."Verse" <= {endVerse}
                   order by ew."PositionInVerse" asc
                   """;

        var verses = await _dbReader.ReadAsListAsync<InteralVerseDto>(sql);
        var list = verses.GroupBy(x => new { x.BibleBookId, x.Chapter, x.Verse })
            .Select(x => new VerseDataList<ElbVerseData>
            {
                BookId = x.Key.BibleBookId,
                Chapter = x.Key.Chapter,
                Verse = x.Key.Verse,
                Data = x.Select(d => new ElbVerseData
                {
                    German = d.Word,
                    Id = d.Id,
                    Order = d.PositionInVerse
                }).OrderBy(o => o.Order).ToList()
            }).OrderBy(x => x.BookId)
            .ThenBy(x => x.Chapter)
            .ThenBy(x => x.Verse)
            .ToList();

        return list;
    }

    internal async Task<IEnumerable<VerseDataList<StepGreekVerseData>>> GetStepGreekVerseDataAsync(int bookId, int chapter, int startVerse, int endVerse)
    {
        var sql = $"""
                   select sgw."Id", sgw."BibleBookId", sgw."Chapter", sgw."Verse", sgw."Greek" "Word", sgw."PositionInVerse", sgw."DisambiguatedStrongs" "Strongs"
                   from "unshackled-word"."StepGreekWords" sgw
                   where   sgw."BibleBookId" = {bookId}
                       and sgw."Chapter" = {chapter}
                       and sgw."Verse" >= {startVerse}
                       and sgw."Verse" <= {endVerse}
                   order by sgw."PositionInVerse" asc
                   """;

        var verses = await _dbReader.ReadAsListAsync<InteralVerseDto>(sql);
        var list = verses.GroupBy(x => new { x.BibleBookId, x.Chapter, x.Verse })
            .Select(x => new VerseDataList<StepGreekVerseData>
            {
                BookId = x.Key.BibleBookId,
                Chapter = x.Key.Chapter,
                Verse = x.Key.Verse,
                Data = x.Select(d => new StepGreekVerseData
                {
                    Greek = d.Word,
                    Id = d.Id,
                    Order = d.PositionInVerse
                }).OrderBy(o => o.Order).ToList()
            }).OrderBy(x => x.BookId)
            .ThenBy(x => x.Chapter)
            .ThenBy(x => x.Verse)
            .ToList();

        return list;
    }

    private class InteralVerseDto
    {
        public int Id { get; set; }
        public int BibleBookId { get; set; }
        public int Chapter { get; set; }
        public int Verse { get; set; }
        public string Word { get; set; } = default!;
        public string? Strongs { get; set; }
        public int PositionInVerse { get; set; }
    }
}
