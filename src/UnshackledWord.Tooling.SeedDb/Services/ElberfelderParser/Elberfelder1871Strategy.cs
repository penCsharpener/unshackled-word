using System.Text;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.Extensions;
using UnshackledWord.Domain.Models.BibleStructure;
using UnshackledWord.Tooling.SeedDb.Services.Abstractions;

namespace UnshackledWord.Tooling.SeedDb.Services.ElberfelderParser;

public sealed class Elberfelder1871Strategy : IFileParserStrategy
{
    private readonly IFileService _fileService;
    private readonly IDbWriter _writer;
    private readonly IDbReader _reader;
    private readonly ILogger<Elberfelder1871Strategy> _logger;
    private int _countVerses;
    private int _countWords;
    private static string nl = Environment.NewLine;
    public List<Elb1871Verse> Elberfelder1871Verses { get; private set; } = new() ;

    public Elberfelder1871Strategy(IFileService fileService, IDbWriter writer, IDbReader reader, ILogger<Elberfelder1871Strategy> logger)
    {
        _fileService = fileService;
        _writer = writer;
        _reader = reader;
        _logger = logger;
    }

    public async Task SaveToDatabase(string filePath, CancellationToken token = default)
    {
        _countVerses = await GetCountVersesAsync(token);
        _countWords = await GetCountWordsAsync(token);

        if (_countVerses > 0 || _countWords > 0)
        {
            _logger.LogInformation("Elberfelder 1871 verses and words already exist in the database. Skipping import. " +
                                   "{countVerses} rows of verses {countWords} rows of words ", _countVerses, _countWords);
            return;
        }

        var lines = await _fileService.ReadAllLinesAsync(filePath, Encoding.UTF8, token);

        foreach (var line in lines)
        {
            var refText = line.Split(" || ");
            var bookRef = refText[0].Split("$");
            var chapterVerse = bookRef[1].Split(":");

            var book = bookRef[0];
            var chapter = int.Parse(chapterVerse[0]);
            var verse = int.Parse(chapterVerse[1]);
            var bookId = BibleBook.AllBooks.First(x => x.Value.Name == book).Key;

            var words = SplitAndSaveIndividualWords(refText[1]).ToList();
            var verseObj = new Elb1871Verse(bookId, chapter, verse, refText[1], words);
            Elberfelder1871Verses.Add(verseObj);
        }

        _logger.LogInformation("Saving split verses to Database.");
        await SaveToDatabaseAsync(Elberfelder1871Verses, 100, token);
    }

    private async Task SaveToDatabaseAsync(List<Elb1871Verse> list, int batchSize, CancellationToken token = default)
    {
        var batch = new List<Elb1871Verse>();

        foreach (var verseChunk in list.Chunk(batchSize))
        {
            if (_countWords == 0)
            {
                await WriteWordsToDbAsync(batch, token);
            }

            if (_countVerses == 0)
            {
                await WriteVersesToDbAsync(batch, token);
            }
        }
    }

    private async Task WriteVersesToDbAsync(List<Elb1871Verse> batch, CancellationToken token = default)
    {
        var rowList = new List<string>();

        foreach (var verse in batch)
        {
            rowList.Add($"({verse.BibleBookId}, {verse.Chapter}, {verse.Verse}, '{verse.Text}')");
        }

        var sql = $"""
                   INSERT INTO "unshackled-word"."Elb1871Verses" ("BibleBookId", "Chapter", "Verse", "VerseText")
                   VALUES
                   {rowList.JoinStrings($",{nl}")};
                   """;

        await _writer.WriteAsync(sql);
    }

    private async Task WriteWordsToDbAsync(List<Elb1871Verse> batch, CancellationToken token = default)
    {
        var rowList = new List<string>();

        foreach (var verse in batch)
        {
            foreach (var word in verse.Words)
            {
                if (word.InContext is "G17-36")
                {
                    continue;
                }

                rowList.Add($"({verse.BibleBookId}, {verse.Chapter}, {verse.Verse}, '{word.InContext}', {word.Order}, '{word.PlainWord}')");
            }
        }

        var sql = $"""
                   INSERT INTO "unshackled-word"."Elb1871Words" ("BibleBookId", "Chapter", "Verse", "WordInContext", "PositionInVerse", "PlainWord")
                   VALUES
                   {rowList.JoinStrings($",{nl}")};
                   """;

        await _writer.WriteAsync(sql);
    }

    private static string CleanUpWord(string word)
    {
        var characters = ",;:.!?\"'{}[]()’".ToCharArray();

        var result = word.Trim();

        foreach (var character in characters)
        {
            result = result.Replace(character.ToString(), string.Empty);
        }

        result = result.Trim('-');

        return result;
    }

    private static IEnumerable<Elb1871Word> SplitAndSaveIndividualWords(string verseText)
    {
        var words = verseText.Split(" ", StringSplitOptions.RemoveEmptyEntries);
        var orderCounter = 1;

        foreach (var word in words)
        {
            var cleanedWord = CleanUpWord(word);

            yield return new Elb1871Word(orderCounter, word, cleanedWord);
            orderCounter++;
        }
    }

    private async Task<int> GetCountVersesAsync(CancellationToken token = default)
    {
        var sql = """
                  select Count(*)
                  from "unshackled-word"."Elb1871Verses"
                  """;

        return await _reader.ExecuteScalarAsync<int>(sql);
    }

    private async Task<int> GetCountWordsAsync(CancellationToken token = default)
    {
        var sql = """
                  select Count(*)
                  from "unshackled-word"."Elb1871Words"
                  """;

        return await _reader.ExecuteScalarAsync<int>(sql);
    }
}
public record Elb1871Verse(int BibleBookId, int Chapter, int Verse, string Text, List<Elb1871Word> Words);
public record Elb1871Word(int Order, string InContext, string PlainWord);
