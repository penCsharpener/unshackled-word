using System.Diagnostics;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.Extensions;
using UnshackledWord.Domain.Models.BibleStructure;
using UnshackledWord.Domain.Models.Dbo;
using UnshackledWord.Infrastructure.Repositories;
using UnshackledWord.Tooling.AiWorker.Models;
using UnshackledWord.Tooling.AiWorker.Models.Greek;

namespace UnshackledWord.Tooling.AiWorker;

public class GreekMappingRepository
{
    private readonly IDbReader _dbReader;
    private readonly IDbWriter _dbWriter;

    public GreekMappingRepository(IDbReader dbReader, IDbWriter dbWriter)
    {
        _dbReader = dbReader;
        _dbWriter = dbWriter;
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

    internal async Task<List<MappingScopeRange>> GetMissingVerseRangesAsync()
    {
        var sql = """
                  select ew."BibleBookId", ew."Chapter", MIN(ew."Verse") MinVerse, MAX(ew."Verse") MaxVerse
                  from "unshackled-word"."Elb1871Words" ew
                    left join "unshackled-word"."Elb1871GreekMapping" egm on ew."Id" = egm."ElbWordId"
                  where ew."BibleBookId" >= 40
                    and egm."ElbWordId" is null
                  group by ew."BibleBookId", ew."Chapter"
                  order by ew."BibleBookId", ew."Chapter"
                  """;

        return (await _dbReader.ReadAsListAsync<MappingScopeRange>(sql)).ToList();
    }

    internal async Task<List<BibleVerseCountingMappingDbo>> GetMissingVersesAsync()
    {
        var sql = $"""
                   select ew."HebRefId", bvcm."LxxRefId"
                   from "unshackled-word"."Elb1871Words" ew
                     inner join {BibleVerseCountingMappingDbo.DboName} bvcm on ew."HebRefId" = bvcm."HebRefId"
                     left join "unshackled-word"."Elb1871GreekMapping" egm on ew."Id" = egm."ElbWordId"
                   where ew."HebRefId" >= 40000000
                     and egm."ElbWordId" is null
                   group by ew."HebRefId", bvcm."LxxRefId"
                   order by ew."HebRefId"
                   """;

        return (await _dbReader.ReadAsListAsync<BibleVerseCountingMappingDbo>(sql)).ToList();
    }

    internal async Task<IEnumerable<BibleReference>> GetGreekNtStructureByChapterAsync(int? bookId, int? chapter, int? verse)
    {
        var sql = """
                  select ew."BibleBookId", ew."Chapter", MAX(ew."Verse") Verse
                  from "unshackled-word"."Elb1871Words" ew
                  where ew."BibleBookId" >= 40
                        and (@BookId is null or ew."BibleBookId" >= @BookId)
                        and (@Chapter is null or ew."Chapter" >= @Chapter)
                        and (@Verse is null or ew."Verse" >= @Verse)
                  group by ew."BibleBookId", ew."Chapter"
                  order by ew."BibleBookId", ew."Chapter"
                  """;

        var parameters = new
        {
            BookId = bookId,
            Chapter = chapter,
            Verse = verse
        };

        return await _dbReader.ReadAsListAsync<BibleReference>(sql, parameters);
    }

    internal async Task<BibleReference> GetLastCompletedVerseAsync()
    {
        const string sql = """
                           select max(egm."HebRefId") "HebRefId"
                           from "unshackled-word"."Elb1871GreekMapping" egm
                           """;

        var resultDb = await _dbReader.ReadFirstOrDefaultAsync<BibleReference?>(sql);

        if (resultDb is null)
        {
            throw new UnreachableException("There should be something in \"unshackled-word\".\"Elb1871GreekMapping\" to continue from");
        }

        var result = resultDb.Value;
        var verseExists = await DoesRefExistAsync(result.BookId, result.Chapter, result.Verse + 1);

        if (verseExists)
        {
            result.Verse++;
            return result;
        }

        var doesChapterExist = await DoesRefExistAsync(result.BookId, result.Chapter + 1, 1);

        if (doesChapterExist)
        {
            result.Chapter++;
            result.Verse = 1;
            return result;
        }

        var doesBookExist = await DoesRefExistAsync(result.BookId + 1, 1, 1);

        if (doesBookExist)
        {
            result.BookId++;
            result.Chapter = 1;
            result.Verse = 1;
            return result;
        }

        throw new UnreachableException("You seemed to have mapped already all the words in the Greek NT for Elberfelder 1871.");
    }

    internal async Task<bool> DoesRefExistAsync(int bookId, int chapter, int verse)
    {
        var startRefId = new BibleReference(bookId, chapter, verse);
        var sqlValidation = $"""
                             select count(*)
                             from "unshackled-word"."Elb1871Words" ew
                             where ew."HebRefId" = {startRefId}
                             """;

        var result = await _dbReader.ExecuteScalarAsync<int>(sqlValidation);

        return result > 0;
    }

    internal async Task<IEnumerable<ElbVerseData>> GetElbVerseDataAsync(int bookId, int chapter, int verse)
    {
        var startRefId = new BibleReference(bookId, chapter, verse);
        var sql = $"""
                   select ew."Id", ew."WordInContext", ew."PositionInVerse"
                   from "unshackled-word"."Elb1871Words" ew
                   where   ew."HebRefId" = {startRefId}
                   order by ew."PositionInVerse" asc
                   """;

        return await _dbReader.ReadAsListAsync<ElbVerseData>(sql);
    }

    internal async Task<List<VerseDataList<ElbVerseData>>> GetElbVerseDataAsync(int[] hebRefIds)
    {
        var sql = $"""
                   select ew."Id", ew."BibleBookId", ew."Chapter", ew."Verse", ew."HebRefId" "RefId", ew."PlainWord" "Word", ew."PositionInVerse"
                   from "unshackled-word"."Elb1871Words" ew
                   where   ew."HebRefId" > 40000000
                       and ew."HebRefId" IN (SELECT * FROM UNNEST(@HebRefIds))
                   order by ew."HebRefId", ew."PositionInVerse" asc
                   """;

        var parameters = new { HebRefIds = hebRefIds };

        var words = await _dbReader.ReadAsListAsync<InternalVerseDto>(sql, parameters);
        var list = words.GroupBy(x => BibleReference.FromRefId(x.RefId))
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

    internal async Task<IEnumerable<StepGreekVerseData>> GetStepGreekVerseDataAsync(int bookId, int chapter, int verse)
    {
        var startRefId = new BibleReference(bookId, chapter, verse).RefId;
        var sql = $"""
                   select sgw."Id", sgw."Greek", sgw."PositionInVerse", sgw."DisambiguatedStrongs"
                   from "unshackled-word"."StepGreekWords" sgw
                       inner join "unshackled-word"."BibleVerseCountingMapping" bvcm ON bvcm."LxxRefId" = sgw."LxxRefId"
                   where   bvcm."HebRefId" = {startRefId}
                   order by sgw."PositionInVerse" asc
                   """;

        return await _dbReader.ReadAsListAsync<StepGreekVerseData>(sql);
    }

    internal async Task<List<VerseDataList<StepGreekVerseData>>> GetStepGreekVerseDataAsync(int[] lxxRefIds)
    {
        var sql = $"""
                   select bvcm."HebRefId" "RefId", sgw."PositionInVerse", sgw."Id", sgw."Greek" "Word"
                   from "unshackled-word"."StepGreekWords" sgw
                       inner join "unshackled-word"."BibleVerseCountingMapping" bvcm on sgw."LxxRefId" = bvcm."LxxRefId"
                   where    sgw."LxxRefId" > 40000000
                        and sgw."LxxRefId" IN (SELECT * FROM UNNEST(@LxxRefIds))
                   order by sgw."LxxRefId", sgw."PositionInVerse" asc
                   """;

        var parameters = new { LxxRefIds = lxxRefIds };

        var words = await _dbReader.ReadAsListAsync<InternalVerseDto>(sql, parameters);
        var list = words.GroupBy(x => BibleReference.FromRefId(x.RefId))
            .Select(x => new VerseDataList<StepGreekVerseData>
            {
                BookId = x.Key.BookId,
                Chapter = x.Key.Chapter,
                Verse = x.Key.Verse,
                RefId = x.Key.RefId,
                Data = x.Select(d => new StepGreekVerseDataWithOrder
                {
                    Greek = d.Word,
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

    internal async Task<List<VerseDataList<ElbVerseData>>> GetElbVerseDataAsync(int bookId, int chapter, int startVerse, int endVerse)
    {
        var sql = $"""
                   select ew."Id", ew."BibleBookId", ew."Chapter", ew."Verse", ew."PlainWord" "Word", ew."PositionInVerse", 0 "PositionInWord"
                   from "unshackled-word"."Elb1871Words" ew
                   where   ew."BibleBookId" = {bookId}
                       and ew."Chapter" = {chapter}
                       and ew."Verse" BETWEEN {startVerse} AND {endVerse}
                   order by ew."PositionInVerse" asc
                   """;

        var verses = await _dbReader.ReadAsListAsync<InternalVerseDto>(sql);
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

    internal async Task<List<VerseDataList<StepGreekVerseData>>> GetStepGreekVerseDataAsync(int bookId, int chapter, int startVerse, int endVerse)
    {
        var startRefId = new BibleReference(bookId, chapter, startVerse).RefId;
        var endRefId = new BibleReference(bookId, chapter, endVerse).RefId;
        var sql = $"""
                   select sgw."Id", sgw."LxxRefId" "RefId", sgw."Greek" "Word", sgw."PositionInVerse", sgw."DisambiguatedStrongs" "Strongs"
                   from "unshackled-word"."StepGreekWords" sgw
                       inner join "unshackled-word"."BibleVerseCountingMapping" bvcm ON bvcm."LxxRefId" = sgw."LxxRefId"
                   where   bvcm."HebRefId" BETWEEN {startRefId} AND {endRefId}
                   order by sgw."LxxRefId", sgw."PositionInVerse" asc
                   """;

        var verses = await _dbReader.ReadAsListAsync<InternalVerseDto>(sql);
        var list = verses.GroupBy(x => BibleReference.FromRefId(x.RefId))
            .Select(x => new VerseDataList<StepGreekVerseData>
            {
                BookId = x.Key.BookId,
                Chapter = x.Key.Chapter,
                Verse = x.Key.Verse,
                RefId = x.Key.RefId,
                Data = x.Select(d => new StepGreekVerseDataWithOrder
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

    public async Task InsertMappingsAsync(IEnumerable<VerseDataList<ElbStepAiMapping>> mappings, IList<ElbVerseData> elbVerses, IList<StepGreekVerseData> stepVerses)
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

                parameters.ElbWordId.Add(wordMap.ElbWordId);
                parameters.StepWordId.Add(wordMap.StepWordId);
                parameters.HebRefId.Add(refId.RefId);
                parameters.IsAddedWord.Add(wordMap.IsAddedWord);
                parameters.ParentGermanWordId.Add(wordMap.ParentElbWordId);
                parameters.PositionInVerse.Add(elbWordOrder);
                parameters.GermanWordPart.Add(wordMap.GermanWordPart);
            }
        }

        var (quotedNames, parameterNames) = PropertyListHelper.GetAllNames(parameters);

         var sql = $"""
                    BEGIN;

                    INSERT INTO "unshackled-word"."Elb1871GreekMapping"
                    ({quotedNames})
                    SELECT *
                    FROM UNNEST({parameterNames})
                    ON CONFLICT ("ElbWordId", "StepWordId") DO NOTHING;

                    COMMIT;
                    """;

        await _dbWriter.WriteAsync(sql, parameters);
    }
}
