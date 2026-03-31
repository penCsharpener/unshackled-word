using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.Extensions;
using UnshackledWord.Tooling.AiWorker.Models;
using UnshackledWord.Tooling.AiWorker.Models.Hebrew;

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

    internal async Task<IEnumerable<BibleBookName>> GetBookNamesAsync()
    {
        var sql = """
                  SELECT bb."Id", bb."Name"
                  FROM "unshackled-word"."BibleBooks" bb
                  """;

        return await _dbReader.ReadAsListAsync<BibleBookName>(sql);
    }

    internal async Task<IEnumerable<BibleReferenceRange>> GetMissingVerseRangesAsync()
    {
        var sql = """
                  select ew."BibleBookId", ew."Chapter", MIN(ew."Verse") MinVerse, MAX(ew."Verse") MaxVerse
                  from "unshackled-word"."Elb1871Words" ew
                    left join "unshackled-word"."Elb1871HebrewMapping" egm on ew."Id" = egm."ElbWordId"
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
                   select ew."Id", ew."BibleBookId", ew."Chapter", ew."Verse", ew."HebRefId", ew."PlainWord" "Word", ew."PositionInVerse"
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

    internal async Task<List<VerseDataList<StepHebrewVerseData>>> GetHebrewVerseDataAsync(int bookId, int chapter, int startVerse, int endVerse)
    {
        var sql = $"""
                   SELECT shw."BibleBookId", shw."Chapter", shw."Verse", shw."PositionInVerse", shw."Id", shw."Hebrew" "Word"
                   FROM "unshackled-word"."StepHebrewWords" shw
                   WHERE (shw."BibleBookId" < 40
                       AND shw."BibleBookId" = {bookId})
                       AND shw."Chapter" = {chapter}
                       AND shw."Verse" >= {startVerse}
                       AND shw."Verse" <= {endVerse}
                   ORDER BY shw."BibleBookId", shw."Chapter", shw."Verse", shw."PositionInVerse"
                   """;

        var verses = await _dbReader.ReadAsListAsync<InternalVerseDto>(sql);
        var list = verses.GroupBy(x => new { x.BibleBookId, x.Chapter, x.Verse })
            .Select(x => new VerseDataList<StepHebrewVerseData>
            {
                BookId = x.Key.BibleBookId,
                Chapter = x.Key.Chapter,
                Verse = x.Key.Verse,
                Data = x.Select(d => new StepHebrewVerseDataWithOrder
                {
                    Hebrew = d.Word,
                    Id = d.Id,
                    Order = d.PositionInVerse,
                    PositionInWord = d.PositionInWord
                }).OrderBy(o => o.Order).ToList()
            }).OrderBy(x => x.BookId)
            .ThenBy(x => x.Chapter)
            .ThenBy(x => x.Verse)
            .ToList();

        return list;
    }

    internal async Task<List<VerseDataList<StepHebrewVerseData>>> GetNormalizedHebrewVerseDataAsync(int bookId, int chapter, int startVerse, int endVerse)
    {
        var sql = $"""
                   SELECT shw."BibleBookId", shw."Chapter", shw."Verse", shw."PositionInVerse", shwn."Id", shwn."Hebrew" "Word", shwnthw."PositionInWord"
                   FROM "unshackled-word"."StepHebrewWordsNormalizedToHebrewWords" shwnthw
                       INNER JOIN "unshackled-word"."StepHebrewWordsNormalized"    shwn    ON shwnthw."StepHebrewWordsNormalizedId" = shwn."Id"
                       INNER  JOIN "unshackled-word"."StepHebrewWords"             shw     ON shwnthw."StepHebrewWordsId" = shw."Id"
                      WHERE (shw."BibleBookId" < 40
                          AND shw."BibleBookId" = {bookId})
                          AND shw."Chapter" = {chapter}
                          AND shw."Verse" >= {startVerse}
                          AND shw."Verse" <= {endVerse}
                   ORDER BY shw."BibleBookId", shw."Chapter", shw."Verse", shw."PositionInVerse"
                   """;

        var verses = await _dbReader.ReadAsListAsync<InternalVerseDto>(sql);
        var list = verses.GroupBy(x => new { x.BibleBookId, x.Chapter, x.Verse })
            .Select(x => new VerseDataList<StepHebrewVerseData>
            {
                BookId = x.Key.BibleBookId,
                Chapter = x.Key.Chapter,
                Verse = x.Key.Verse,
                Data = x.Select(d => new StepHebrewVerseDataWithOrder()
                {
                    Hebrew = d.Word,
                    Id = d.Id,
                    Order = d.PositionInVerse,
                    PositionInWord = d.PositionInWord
                }).OrderBy(o => o.Order).ToList()
            }).OrderBy(x => x.BookId)
            .ThenBy(x => x.Chapter)
            .ThenBy(x => x.Verse)
            .ToList();

        return list;
    }

    public async Task InsertMappingsAsync(IEnumerable<VerseDataList<ElbStepAiMapping>> mappings,
        IList<ElbVerseData> elbVerses,
        IList<StepHebrewVerseData> stepVerses)
    {
        var sb = new List<string>();

        foreach (var mapping in mappings)
        {
            foreach (var wordMap in mapping.Data)
            {
                var stepId = wordMap.StepWordId?.ToString() ?? "null";
                var parentId = wordMap.ParentElbWordId?.ToString() ?? "null";
                var foundWord = elbVerses.FirstOrDefault(x => x.Id == wordMap.ElbWordId);
                var elbWordOrder = foundWord?.Order ?? 999;
                var germanWord = foundWord?.German;
                var foundGreek = stepVerses.FirstOrDefault(x => wordMap.StepWordId is not null && x.Id == wordMap.StepWordId);
                var greekWord = foundGreek?.Hebrew;
                sb.Add($"({wordMap.ElbWordId}, {stepId}, {mapping.BookId}, {mapping.Chapter}," +
                       $" {mapping.Verse}, {wordMap.IsAddedWord}, {parentId}, {elbWordOrder} /* {germanWord} - {greekWord} */)");
            }
        }

        var sql = $"""
                   INSERT INTO "unshackled-word"."Elb1871HebrewMapping"
                   ("ElbWordId","StepWordId","BookId","Chapter","Verse","IsAddedWord","ParentGermanWordId","PositionInVerse")
                   VALUES
                   {sb.JoinStrings($",{Environment.NewLine}")}
                   ON CONFLICT ("ElbWordId") DO NOTHING
                   """;

        await _dbWriter.WriteAsync(sql);
    }
}
