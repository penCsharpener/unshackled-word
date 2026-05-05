using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.Extensions;
using UnshackledWord.Domain.Models.BibleStructure;
using UnshackledWord.Domain.Models.Dbo;
using UnshackledWord.Tooling.SeedDb.Services.Abstractions;

namespace UnshackledWord.Tooling.SeedDb.Services.ElberfelderParser;

public class HyphenFixRunner : IRunner
{
    private readonly IDbWriter _writer;
    private readonly IDbReader _reader;
    private readonly HyphenTypoDetectionService _typoService;
    private readonly ILogger<HyphenFixRunner> _logger;

    public HyphenFixRunner(IDbWriter writer, IDbReader reader, HyphenTypoDetectionService typoService,
        ILogger<HyphenFixRunner> logger)
    {
        _writer = writer;
        _reader = reader;
        _typoService = typoService;
        _logger = logger;
    }

    public async Task Run(CancellationToken token = default)
    {
        var verseFindings = await _typoService.GetHyphenWordsAsync(token);

        var totalWords = new List<Elb1871WordDbo>();
        var id = 1;
        var newElbId = 999999;

        foreach (var verseFinding in verseFindings)
        {
            var lineItem = new ElbExportLineItem(verseFinding.HebRefId, verseFinding.OriginalVerse);
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

        foreach (var verse in totalWords.GroupBy(x => x.HebRefId))
        {
            var findings = verseFindings.FirstOrDefault(x => x.HebRefId == verse.Key);
            var hyphenedWords = await GetWordsFromDbAsync(verse.Key, token);

            if (hyphenedWords.Count == 0)
            {
                _logger.LogWarning("No words found in the database for HebRefId {HebRefId}. Skipping.", verse.Key);
                continue;
            }

            if (findings is null)
            {
                _logger.LogWarning("No wrongly hyphened words found for HebRefId {HebRefId}. Skipping.", verse.Key);
                continue;
            }

            var onlyHyphenedWords = hyphenedWords
                .Where(word => word.WordInContext.Contains('-')
                               && word.PlainWord.IsNotNullOrEmpty()
                               && !ValidHyphenWords.NonPlaceNames.Contains(word.PlainWord)
                               && word.WordInContext.Length > 1).ToList();

            if (onlyHyphenedWords.Count != findings.HyphenFindings.Count(x => !x.IsPlaceName))
            {
                _logger.LogWarning(
                    "Mismatch in the number of hyphened words found in the database and the number of findings for HebRefId {HebRefId}. Found {DbCount} hyphened words in DB and {FindingCount} findings.",
                    verse.Key, onlyHyphenedWords.Count, findings.HyphenFindings.Count);
                throw new Exception();
            }

            foreach (var word in onlyHyphenedWords)
            {
                var wordFinding = findings?.HyphenFindings.FirstOrDefault(x => x.HyphenPlainWord == word.PlainWord);

                if (wordFinding is null)
                {
                    _logger.LogWarning(
                        "No finding found for word '{word}' in HebRefId {HebRefId} at position {PositionInVerse}. Skipping.",
                        word.WordInContext, word.HebRefId, word.PositionInVerse);
                    throw new Exception();
                }

                if (word.WordInContext.Count(x => x == '-') == 1)
                {
                    if (word.WordInContext.StartsWith('-'))
                    {
                        continue;
                    }

                    if (word.WordInContext.EndsWith('-'))
                    {
                        continue;
                    }
                }

                _logger.LogInformation("Processing word '{word}' in HebRefId {HebRefId} at position {PositionInVerse}.",
                    word.WordInContext, word.HebRefId, word.PositionInVerse);

                await IncrementPositionInLaterVersesAsync(word, hyphenedWords, 2);

                var parts = word.WordInContext
                    .Replace("Beth-Horon", "Beth_Horon")
                    .Replace("-:", ": ")
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

                var wordPart = parts[0].Replace("Beth_Horon", "Beth-Horon");

                var updateFirstParameters = new
                {
                    word.HebRefId,
                    word.PositionInVerse,
                    WordInContext = wordPart,
                    PlainWord = wordPart.RemovePunctuation()
                };

                _logger.LogDebug(
                    "Updating word {word} in HebRefId {HebRefId} at position {oldPosition} to new WordInContext '{WordInContext}' and PlainWord '{PlainWord}'.",
                    word.WordInContext, word.HebRefId, word.PositionInVerse, updateFirstParameters.WordInContext,
                    updateFirstParameters.PlainWord);

                await WriteAsync(updateFirstPart, updateFirstParameters);

                var bibRef = BibleReference.FromRefId(word.HebRefId);

                for (var i = 0; i < parts.Skip(1).Count(); i++)
                {
                    newElbId++;
                    var part = parts[i + 1];
                    var parameter = new
                    {
                        word.HebRefId,
                        BibleBookId = bibRef.BookId,
                        bibRef.Chapter,
                        bibRef.Verse,
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

    private async Task IncrementPositionInLaterVersesAsync(Elb1871WordDbo wordWithHyphen,
        ICollection<Elb1871WordDbo> wordsInVerse, int offset)
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

            _logger.LogDebug(
                "Shifting word {word} with Id {WordId} in HebRefId {HebRefId} at position {oldPosition} to new position {newPosition}.",
                wordToUpdate.WordInContext, wordToUpdate.Id, shiftParameters.HebRefId, shiftParameters.PositionInVerse,
                shiftParameters.NewPosition);
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
