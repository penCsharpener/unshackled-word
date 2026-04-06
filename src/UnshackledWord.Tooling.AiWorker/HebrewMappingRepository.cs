using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.Models.BibleStructure;
using UnshackledWord.Domain.Models.Dbo;
using UnshackledWord.Infrastructure.Repositories;
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

    internal async Task<IEnumerable<MappingScopeRange>> GetMissingVerseRangesAsync()
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

        return await _dbReader.ReadAsListAsync<MappingScopeRange>(sql);
    }

    internal async Task<List<BibleVerseCountingMappingDbo>> GetMissingVersesAsync()
    {
        var sql = $"""
                   select ew."HebRefId", bvcm."LxxRefId"
                   from "unshackled-word"."Elb1871Words" ew
                     inner join {BibleVerseCountingMappingDbo.DboName} bvcm on ew."HebRefId" = bvcm."HebRefId"
                     left join "unshackled-word"."Elb1871HebrewMapping" egm on ew."Id" = egm."ElbWordId"
                   where ew."HebRefId" <= 40000000
                     and egm."ElbWordId" is null
                   group by ew."HebRefId", bvcm."LxxRefId"
                   order by ew."HebRefId"
                   """;

        return (await _dbReader.ReadAsListAsync<BibleVerseCountingMappingDbo>(sql)).ToList();
    }

    internal async Task<List<VerseDataList<ElbVerseData>>> GetElbVerseDataAsync(int p1, int p2, int p3, int p4)
    {
        return [];
    }

    internal async Task<List<VerseDataList<ElbVerseData>>> GetElbVerseDataAsync(int[] hebRefIds)
    {
        var sql = $"""
                   select ew."Id", ew."BibleBookId", ew."Chapter", ew."Verse", ew."HebRefId" "RefId", ew."PlainWord" "Word", ew."PositionInVerse"
                   from "unshackled-word"."Elb1871Words" ew
                   where   ew."HebRefId" < 40000000
                       and ew."HebRefId" IN (SELECT * FROM UNNEST(@HebRefIds))
                   order by ew."HebRefId", ew."PositionInVerse" asc
                   """;

        var parameters = new { HebRefIds = hebRefIds };

        var verses = await _dbReader.ReadAsListAsync<InternalVerseDto>(sql, parameters);
        var list = verses.GroupBy(x => BibleReference.FromRefId(x.RefId))
            .Select(x => new VerseDataList<ElbVerseData>
            {
                BookId = x.Key.BookId,
                Chapter = x.Key.Chapter,
                Verse = x.Key.Verse,
                RefId = x.Key.RefId,
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

    internal async Task<List<VerseDataList<StepHebrewVerseData>>> GetStepHebrewVerseDataAsync(int[] lxxRefIds)
    {
        var sql = $"""
                   select bvcm."HebRefId" "RefId", shw."PositionInVerse", shw."Id", shw."Hebrew" "Word"
                   from "unshackled-word"."StepHebrewWords" shw
                       inner join "unshackled-word"."BibleVerseCountingMapping" bvcm on shw."LxxRefId" = bvcm."LxxRefId"
                   where    shw."LxxRefId" < 40000000
                        and shw."LxxRefId" IN (SELECT * FROM UNNEST(@LxxRefIds))
                   order by shw."LxxRefId", shw."PositionInVerse" asc
                   """;

        var parameters = new { LxxRefIds = lxxRefIds };

        var words = await _dbReader.ReadAsListAsync<InternalVerseDto>(sql, parameters);
        var list = words.GroupBy(x => BibleReference.FromRefId(x.RefId))
            .Select(x => new VerseDataList<StepHebrewVerseData>
            {
                BookId = x.Key.BookId,
                Chapter = x.Key.Chapter,
                Verse = x.Key.Verse,
                RefId = x.Key.RefId,
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

    internal async Task<List<VerseDataList<StepHebrewVerseData>>> GetHebrewVerseDataAsync(int bookId, int chapter, int startVerse, int endVerse)
    {
        var startRefId = new BibleReference(bookId, chapter, startVerse).RefId;
        var endRefId = new BibleReference(bookId, chapter, endVerse).RefId;
        var sql = $"""
                   SELECT shw."LxxRefId", shw."PositionInVerse", shw."Id", shw."Hebrew" "Word"
                   FROM "unshackled-word"."StepHebrewWords" shw
                   WHERE   shw."LxxRefId" < 40000000
                       AND shw."LxxRefId" >= {startRefId}
                       AND shw."LxxRefId" <= {endRefId}
                   ORDER BY shw."LxxRefId", shw."PositionInVerse"
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
        var startRefId = new BibleReference(bookId, chapter, startVerse).RefId;
        var endRefId = new BibleReference(bookId, chapter, endVerse).RefId;
        var sql = $"""
                   SELECT shw."LxxRefId", shw."PositionInVerse", shwn."Id", shwn."Hebrew" "Word", shwnthw."PositionInWord"
                   FROM "unshackled-word"."StepHebrewWordsNormalizedToHebrewWords" shwnthw
                       INNER JOIN "unshackled-word"."StepHebrewWordsNormalized"    shwn    ON shwnthw."StepHebrewWordsNormalizedId" = shwn."Id"
                       INNER  JOIN "unshackled-word"."StepHebrewWords"             shw     ON shwnthw."StepHebrewWordsId" = shw."Id"
                   WHERE  shw."LxxRefId" < 40000000
                      AND shw."LxxRefId" >= {startRefId}
                      AND shw."LxxRefId" <= {endRefId}
                   ORDER BY shw."LxxRefId", shw."PositionInVerse"
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
        var parameters = new
        {
            ElbWordId = new List<int>(),
            StepWordId = new List<int?>(),
            HebRefId = new List<int>(),
            IsAddedWord = new List<bool>(),
            ParentGermanWordId = new List<int?>(),
            PositionInVerse = new List<int>(),
            GermanWordPart = new List<string?>(),
        };

        foreach (var mapping in mappings)
        {
            foreach (var wordMap in mapping.Data)
            {
                var refId = new BibleReference(mapping.BookId, mapping.Chapter, mapping.Verse);
                var foundWord = elbVerses.FirstOrDefault(x => x.Id == wordMap.ElbWordId);
                var elbWordOrder = foundWord?.Order ?? 999;
                var germanWord = wordMap?.GermanWordPart;

                parameters.ElbWordId.Add(wordMap.ElbWordId);
                parameters.StepWordId.Add(wordMap.StepWordId);
                parameters.HebRefId.Add(refId.RefId);
                parameters.IsAddedWord.Add(wordMap.IsAddedWord);
                parameters.ParentGermanWordId.Add(wordMap.ParentElbWordId);
                parameters.PositionInVerse.Add(elbWordOrder);
                parameters.GermanWordPart.Add(germanWord);
            }
        }

        var (quotedNames, parameterNames) = PropertyListHelper.GetAllNames(parameters);

        var sql = $"""
                   INSERT INTO "unshackled-word"."Elb1871HebrewMapping"
                   ({quotedNames})
                   SELECT *
                   FROM UNNEST({parameterNames})
                   ON CONFLICT ("ElbWordId", "StepWordId") DO NOTHING;
                   """;

        await _dbWriter.WriteAsync(sql, parameters);
    }
}
