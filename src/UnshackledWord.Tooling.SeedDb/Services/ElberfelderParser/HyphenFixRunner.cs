using System.Text;
using Microsoft.Extensions.Options;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.Extensions;
using UnshackledWord.Domain.Models.BibleStructure;
using UnshackledWord.Domain.Models.Dbo;
using UnshackledWord.Domain.Models.Settings;
using UnshackledWord.Tooling.SeedDb.Services.Abstractions;

namespace UnshackledWord.Tooling.SeedDb.Services.ElberfelderParser;

public class HyphenFixRunner : IRunner
{
    private readonly IFileService _fileService;
    private readonly IDbWriter _writer;
    private readonly IDbReader _reader;
    private readonly DatabaseSeedSettings _options;
    private readonly ILogger<HyphenFixRunner> _logger;
    private static string[] _hyphenIgnoreWords = new[] { "Beth-Horon", "Eglath-Schelischija", "Hazar-Enon" };

    public HyphenFixRunner(IFileService fileService, IDbWriter writer, IDbReader reader, IOptions<AppSettings> options, ILogger<HyphenFixRunner> logger)
    {
        _fileService = fileService;
        _writer = writer;
        _reader = reader;
        _options = options.Value.DatabaseSeeding;
        _logger = logger;
    }

    public async Task Run(CancellationToken token = default)
    {
        var filePath = _fileService.Combine(_options.SolutionAssetsPath, "elberfelder1871-theword-export_corrections.txt");

        var totalWords = new List<Elb1871WordDbo>();
        var lines = await _fileService.ReadAllLinesAsync(filePath, Encoding.UTF8, token);
        var id = 1;
        var newElbId = 999999;

        foreach (var line in lines)
        {
            var lineItem = new ElbExportLineItem(line);
            var wordDbos = lineItem.Words.Select(x =>
            {
                var word = new Elb1871WordDbo
                {
                    Id = id,
                    BibleBookId = lineItem.HebRefId.BookId,
                    Chapter = lineItem.HebRefId.Chapter,
                    Verse = lineItem.HebRefId.Verse,
                    HebRefId = lineItem.HebRefId.RefId,
                    WordInContext = x.InContext,
                    PlainWord = x.PlainWord,
                    PositionInVerse = x.Order
                };
                id++;

                return word;
            }).ToList();

            totalWords.AddRange(wordDbos);
        }

        _logger.LogInformation(totalWords.Select(x => x.HebRefId.ToString()).Distinct().JoinStrings(","));

        foreach (var verse in totalWords.GroupBy(x => x.HebRefId))
        {
            var hyphenedWords = await GetWordsFromDbAsync(verse.Key, token);

            if (hyphenedWords.Count == 0)
            {
                _logger.LogWarning("No words found in the database for HebRefId {HebRefId}. Skipping.", verse.Key);
                continue;
            }

            var fixedWords = verse.OrderByDescending(x => x.PositionInVerse).ToList();

            if (hyphenedWords.Count >= fixedWords.Count)
            {
                _logger.LogWarning("fixed words must be at least one word longer than the original verses with hyphened words.");
                continue;
            }

            var onlyHyphenedWords = hyphenedWords
                .Where(word => word.WordInContext.Contains('-')
                               && !_hyphenIgnoreWords.Contains(word.PlainWord)
                               && word.WordInContext.Length > 1).ToList();

            foreach (var word in onlyHyphenedWords)
            {
                _logger.LogInformation("Processing word '{word}' in HebRefId {HebRefId} at position {PositionInVerse}.", word.WordInContext, word.HebRefId, word.PositionInVerse);

                await IncrementPositionInLaterVersesAsync(word, hyphenedWords, 2);

                var parts = word.WordInContext
                    .Replace("Beth-Horon", "Beth_Horon")
                    .Replace("Eglath-Schelischija", "Eglath_Schelischija")
                    .Replace("Hazar-Enon", "Hazar_Enon")
                    .Replace("-", " - ").Split(' ',
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                if (parts.Length > 3)
                {
                    throw new Exception(
                        $"{word.HebRefId}:{word.PositionInVerse} This words needs special handling {word.WordInContext}");
                }

                var updateFirstPart = """
                                      UPDATE "unshackled-word"."Elb1871Words"
                                      SET "WordInContext" = @WordInContext, "PlainWord" = @PlainWord
                                      WHERE "HebRefId" = @HebRefId
                                        AND "PositionInVerse" = @PositionInVerse;

                                      UPDATE "unshackled-word"."ElbMorphologyRaw"
                                      SET "Lemma" = '######', "PartOfSpeech" = '------'
                                      WHERE "HebRefId" = @HebRefId
                                        AND "PositionInVerse" = @PositionInVerse;
                                      """;

                var wordPart = parts[0].Replace("Beth_Horon", "Beth-Horon")
                    .Replace("Eglath_Schelischija", "Eglath-Schelischija")
                    .Replace("Hazar_Enon", "Hazar-Enon");

                var updateFirstParameters = new
                {
                    word.HebRefId, word.PositionInVerse, WordInContext = wordPart, PlainWord = wordPart.RemovePunctuation()
                };

                _logger.LogDebug(
                    "Updating word {word} in HebRefId {HebRefId} at position {oldPosition} to new WordInContext '{WordInContext}' and PlainWord '{PlainWord}'.",
                    word.WordInContext, word.HebRefId, word.PositionInVerse, updateFirstParameters.WordInContext, updateFirstParameters.PlainWord);

                await WriteAsync(updateFirstPart, updateFirstParameters);

                var bibRef = BibleReference.FromRefId(word.HebRefId);

                for (int i = 0; i < parts.Skip(1).Count(); i++)
                {
                    newElbId++;
                    var part = parts[i + 1];
                    var parameter = new
                    {
                        HebRefId = word.HebRefId,
                        BibleBookId = bibRef.BookId,
                        Chapter = bibRef.Chapter,
                        Verse = bibRef.Verse,
                        WordInContext = part,
                        PlainWord = part.RemovePunctuation(),
                        PositionInVerse = word.PositionInVerse + i + 1,
                        ElbWordId = newElbId,
                        Lemma = "######",
                        PartOfSpeech = "------",
                    };

                    var sqlInsertElbWords = $"""
                                             INSERT INTO "unshackled-word"."Elb1871Words" ("HebRefId", "BibleBookId", "Chapter", "Verse", "WordInContext", "PlainWord", "PositionInVerse")
                                             VALUES
                                             (@HebRefId,@BibleBookId,@Chapter,@Verse,@WordInContext,@PlainWord,@PositionInVerse);
                                             """;

                    await WriteAsync(sqlInsertElbWords, parameter);

//                     var sqlInsertGreekMapping = $"""
//                                              INSERT INTO "unshackled-word"."Elb1871GreekMapping"
//                                              ("HebRefId", "PositionInVerse", "ElbWordId")
//                                              VALUES
//                                              (@HebRefId,@PositionInVerse,@ElbWordId);
//                                              """;
//
//                     await WriteAsync(sqlInsertGreekMapping, parameter);

//                     var sqlInsertHebrewMapping = $"""
//                                                   INSERT INTO "unshackled-word"."Elb1871HebrewMapping"
//                                                   ("HebRefId", "PositionInVerse", "ElbWordId")
//                                                   VALUES
//                                                   (@HebRefId,@PositionInVerse,@ElbWordId);
//                                                   """;
//
//                     await WriteAsync(sqlInsertHebrewMapping, parameter);

                    var sqlInsertMorphology = $"""
                                                  INSERT INTO "unshackled-word"."ElbMorphologyRaw"
                                                  ("HebRefId", "PositionInVerse", "Lemma", "PartOfSpeech")
                                                  VALUES
                                                  (@HebRefId,@PositionInVerse,@Lemma,@PartOfSpeech);
                                                  """;

                    await WriteAsync(sqlInsertMorphology, parameter);

                    _logger.LogDebug(
                        "Inserting new word for HebRefId {HebRefId} at position {PositionInVerse} with WordInContext '{WordInContext}' and PlainWord '{PlainWord}'.",
                        parameter.HebRefId, parameter.PositionInVerse,
                        parameter.WordInContext, parameter.PlainWord);
                }
            }
        }
    }

    private async Task IncrementPositionInLaterVersesAsync(Elb1871WordDbo wordWithHyphen, ICollection<Elb1871WordDbo> wordsInVerse, int offset)
    {
        var wordsToUpdate = wordsInVerse.Where(x => x.PositionInVerse > wordWithHyphen.PositionInVerse)
            .OrderByDescending(x => x.PositionInVerse).ToList();

        foreach (var wordToUpdate in wordsToUpdate)
        {
            var shiftParameters = new
            {
                wordWithHyphen.HebRefId,
                wordToUpdate.PositionInVerse,
                NewPosition = wordToUpdate.PositionInVerse + offset
            };

            var shiftElbWords = """
                                UPDATE "unshackled-word"."Elb1871Words"
                                SET "PositionInVerse" = @NewPosition
                                WHERE "HebRefId" = @HebRefId
                                  AND "PositionInVerse" = @PositionInVerse;
                                """;
            await WriteAsync(shiftElbWords, shiftParameters);

            var shiftGreekMapping = """
                                    UPDATE "unshackled-word"."Elb1871GreekMapping"
                                    SET "PositionInVerse" = @NewPosition
                                    WHERE "HebRefId" = @HebRefId
                                      AND "PositionInVerse" = @PositionInVerse;
                                    """;
            await WriteAsync(shiftGreekMapping, shiftParameters);

            var shiftHebrewMapping = """
                                    UPDATE "unshackled-word"."Elb1871HebrewMapping"
                                    SET "PositionInVerse" = @NewPosition
                                    WHERE "HebRefId" = @HebRefId
                                      AND "PositionInVerse" = @PositionInVerse;
                                    """;
            await WriteAsync(shiftHebrewMapping, shiftParameters);

            var shiftMorphology = """
                                    UPDATE "unshackled-word"."ElbMorphologyRaw"
                                    SET "PositionInVerse" = @NewPosition
                                    WHERE "HebRefId" = @HebRefId
                                      AND "PositionInVerse" = @PositionInVerse;
                                    """;
            await WriteAsync(shiftMorphology, shiftParameters);

            _logger.LogDebug("Shifting word {word} with Id {WordId} in HebRefId {HebRefId} at position {oldPosition} to new position {newPosition}.", wordToUpdate.WordInContext, wordToUpdate.Id, shiftParameters.HebRefId, shiftParameters.PositionInVerse, shiftParameters.NewPosition);
            wordToUpdate.PositionInVerse += offset;
        }
    }

    private async Task<ICollection<Elb1871WordDbo>> GetWordsFromDbAsync(int hebRefId, CancellationToken token)
    {
        var sql = """
                  SELECT *
                  FROM "unshackled-word"."Elb1871Words" ew
                  WHERE ew."HebRefId" = @HebRefId
                  ORDER BY ew."PositionInVerse" ASC;
                  """;

        return (await _reader.ReadAsListAsync<Elb1871WordDbo>(sql, new { HebRefId = hebRefId })).ToList();
    }

    private async Task WriteAsync(string sql, object parameters)
    {
        await _writer.WriteAsync(WrapInTransaction(sql), parameters);
    }

    private static string WrapInTransaction(string sql)
    {
        return $"""
                BEGIN;
                {sql};
                COMMIT;
                """;
    }
}
