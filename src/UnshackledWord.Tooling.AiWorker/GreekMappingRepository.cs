using System.Diagnostics;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.Extensions;
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

    internal async Task<IEnumerable<BibleReferenceRange>> GetMissingVerseRangesAsync()
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

        return await _dbReader.ReadAsListAsync<BibleReferenceRange>(sql);
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
                           select max(egm."BookId") "BibleBookId", max(egm."Chapter") "Chapter", max(egm."Verse") "Verse"
                           from "unshackled-word"."Elb1871GreekMapping" egm
                           """;

        var result = await _dbReader.ReadFirstOrDefaultAsync<BibleReference>(sql);

        if (result is null)
        {
            throw new UnreachableException("There should be something in \"unshackled-word\".\"Elb1871GreekMapping\" to continue from");
        }

        var verseExists = await DoesRefExistAsync(result.BibleBookId, result.Chapter, result.Verse + 1);

        if (verseExists)
        {
            result.Verse++;
            return result;
        }

        var doesChapterExist = await DoesRefExistAsync(result.BibleBookId, result.Chapter + 1, 1);

        if (doesChapterExist)
        {
            result.Chapter++;
            result.Verse = 1;
            return result;
        }

        var doesBookExist = await DoesRefExistAsync(result.BibleBookId + 1, 1, 1);

        if (doesBookExist)
        {
            result.BibleBookId++;
            result.Chapter = 1;
            result.Verse = 1;
            return result;
        }

        throw new UnreachableException("You seemed to have mapped already all the words in the Greek NT for Elberfelder 1871.");
    }

    internal async Task<bool> DoesRefExistAsync(int bookId, int chapter, int verse)
    {
        var sqlValidation = $"""
                             select count(*)
                             from "unshackled-word"."Elb1871Words" ew
                             where ew."BibleBookId" = {bookId}
                                 and ew."Chapter" = {chapter}
                                 and ew."Verse" = {verse}
                             """;

        var result = await _dbReader.ExecuteScalarAsync<int>(sqlValidation);

        return result > 0;
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

    internal async Task<List<VerseDataList<ElbVerseData>>> GetElbVerseDataAsync(int bookId, int chapter, int startVerse, int endVerse)
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

    internal async Task<List<VerseDataList<StepGreekVerseData>>> GetStepGreekVerseDataAsync(int bookId, int chapter, int startVerse, int endVerse)
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

        var verses = await _dbReader.ReadAsListAsync<InternalVerseDto>(sql);
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

    public async Task InsertMappingsAsync(IEnumerable<VerseDataList<ElbStepAiMapping>> mappings, IList<ElbVerseData> elbVerses, IList<StepGreekVerseData> stepVerses)
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
                var greekWord = foundGreek?.Greek;
                sb.Add($"({wordMap.ElbWordId}, {stepId}, {mapping.BookId}, {mapping.Chapter}, {mapping.Verse}, " +
                       $"{wordMap.IsAddedWord}, {parentId}, {elbWordOrder} /* {germanWord} - {greekWord} */)");
            }
        }

        var sql = $"""
                   INSERT INTO "unshackled-word"."Elb1871GreekMapping"
                   ("ElbWordId","StepGreekId","BookId","Chapter","Verse","IsAddedWord","ParentGermanWordId","WordOrderInVerse")
                   VALUES
                   {sb.JoinStrings($",{Environment.NewLine}")}
                   ON CONFLICT ("ElbWordId") DO NOTHING
                   """;

        await _dbWriter.WriteAsync(sql);
    }
}
