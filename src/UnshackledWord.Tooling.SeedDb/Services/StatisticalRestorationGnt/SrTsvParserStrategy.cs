using System.Text;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.Extensions;
using UnshackledWord.Tooling.SeedDb.Models;
using UnshackledWord.Tooling.SeedDb.Services.Abstractions;

namespace UnshackledWord.Tooling.SeedDb.Services.StatisticalRestorationGnt;

public sealed class SrTsvParserStrategy : IFileParserStrategy
{
    private readonly IFileService _fileService;
    private readonly IDbWriter _dbWriter;
    private readonly IDbReader _reader;
    private readonly ParseHelper _parseHelper;

    public SrTsvParserStrategy(IFileService fileService, IDbWriter dbWriter, IDbReader reader, ParseHelper parseHelper)
    {
        _fileService = fileService;
        _dbWriter = dbWriter;
        _reader = reader;
        _parseHelper = parseHelper;
    }

    public async Task SaveToDatabase(string filePath, CancellationToken token = default)
    {
        var select = """
                     SELECT *
                     FROM "unshackled-word"."GntHotWords"
                     LIMIT 1;
                     """;
        var existingRows = await _reader.ReadFirstOrDefaultAsync<object>(select);

        if (existingRows is not null)
        {
            return;
        }

        var lines = await _fileService.ReadAllLinesAsync(filePath, Encoding.UTF8, token);
        var wordList = new List<WordInfo>();

        foreach (var line in lines.Skip(1))
        {
            var parts = line.Split('\t');
            if (parts.Length < 7)
            {
                continue;
            }

            var bibleBook = _parseHelper.ParseNtBookId(parts[0]);
            var reference = _parseHelper.ParseNtVerseId(parts[0]);
            var wordInContext = parts[1];
            var koineWord = parts[2];
            var lemma = parts[3];
            var strongs = parts[4];
            var partOfSpeech = parts[5];
            var conjugation = parts[6];

            var wordInfo = new WordInfo(
                bibleBook,
                bibleBook.Id,
                reference.Chapter,
                reference.Verse,
                wordInContext,
                koineWord,
                lemma,
                strongs,
                partOfSpeech,
                conjugation
            );

            wordList.Add(wordInfo);
        }

        await InsertAsync(wordList, 50);
    }

    private async Task InsertAsync(WordInfo wordInfo)
    {
        var sql = """
                  INSERT INTO "unshackled-word"."GntHotWords"
                  ("BibleBookId"", "Chapter", "Verse", "WordInContext", "Koine", "Lemma", "Strongs", "PartOfSpeech", "GrammaticalKey")
                  VALUES (@BibleBookId, @WordInContext, @Koine, @Lemma, @Strongs, @PartOfSpeech, @ConjugationKey);
                  """;

        await _dbWriter.WriteAsync(sql, wordInfo);
    }

    private async Task InsertAsync(List<WordInfo> wordInfos, int bulkSize)
    {
        var sql = """
                  INSERT INTO "unshackled-word"."GntHotWords"
                  ("BibleBookId", "Chapter", "Verse", "WordInContext", "Koine", "Lemma", "Strongs", "PartOfSpeech", "GrammaticalKey")
                  VALUES
                  """;

        var counter = 1;
        var batch = new List<string>();

        foreach (var wordInfo in wordInfos)
        {
            batch.Add($"({wordInfo.BibleBookId}, {wordInfo.Chapter}, {wordInfo.Verse}, '{wordInfo.WordInContext}', " +
                      $"'{wordInfo.Koine}', '{wordInfo.Lemma}', '{wordInfo.Strongs}', " +
                      $"'{wordInfo.PartOfSpeech}', '{wordInfo.ConjugationKey}')");
            counter++;

            if (counter == bulkSize)
            {
                await WriteBatch(sql, batch);

                batch.Clear();
                counter = 1;
            }
        }

        await WriteBatch(sql, batch);
    }

    private async Task WriteBatch(string command, List<string> rows)
    {
        var insert = command + Environment.NewLine + rows.JoinStrings($",{Environment.NewLine}") + ";";
        await _dbWriter.WriteAsync(insert);
    }
}
