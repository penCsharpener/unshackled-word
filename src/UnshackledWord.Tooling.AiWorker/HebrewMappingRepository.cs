using UnshackledWord.Application.Abstractions;
using UnshackledWord.Tooling.AiWorker.Models;

namespace UnshackledWord.Tooling.AiWorker;

public class HebrewMappingRepository
{
    private readonly IDbReader _dbReader;
    private readonly IDbWriter _dbWriter;

    public HebrewMappingRepository(IDbReader dbReader, IDbWriter dbWriter)
    {
        _dbReader = dbReader;
        _dbWriter = dbWriter;
    }

    internal async Task<IEnumerable<BibleReferenceRange>> GetMissingVerseRangesAsync()
    {
        var sql = """
                  select ew."BibleBookId", ew."Chapter", MIN(ew."Verse") MinVerse, MAX(ew."Verse") MaxVerse
                  from "unshackled-word"."Elb1871Words" ew
                    left join "unshackled-word"."Elb1871GreekMapping" egm on ew."Id" = egm."ElbWordId"
                  where ew."BibleBookId" < 40
                    and egm."ElbWordId" is null
                  group by ew."BibleBookId", ew."Chapter"
                  order by ew."BibleBookId", ew."Chapter"
                  """;

        return await _dbReader.ReadAsListAsync<BibleReferenceRange>(sql);
    }

    internal async Task<List<VerseDataList<ElbVerseData>>> GetElbVerseDataAsync(int bookId, int chapter, int startVerse, int endVerse)
    {
        var sql = $"""
                   select ew."Id", ew."BibleBookId", ew."Chapter", ew."Verse", ew."WordInContext" "Word", ew."PositionInVerse"
                   from "unshackled-word"."Elb1871Words" ew
                   where  (ew."BibleBookId" < 40
                       and ew."BibleBookId" = {bookId})
                       and ew."Chapter" = {chapter}
                       and ew."Verse" >= {startVerse}
                       and ew."Verse" <= {endVerse}
                   order by ew."PositionInVerse" asc
                   """;

        var verses = await _dbReader.ReadAsListAsync<InternalVerseDto>(sql);
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


}
