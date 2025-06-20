using System.Text;
using Microsoft.Extensions.Options;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.Extensions;
using UnshackledWord.Domain.Models.BibleStructure;
using UnshackledWord.Domain.Models.Settings;
using UnshackledWord.Tooling.SeedDb.Services.Abstractions;

namespace UnshackledWord.Tooling.SeedDb.Services.ElberfelderParser;

public sealed class Elberfelder1871Strategy : IFileParserStrategy
{
    private readonly IFileService _fileService;
    private readonly IDbWriter _writer;
    private readonly IDbReader _reader;
    private readonly DatabaseSeedSettings _options;
    private static string nl = Environment.NewLine;
    public List<Elb1871Verse> Elberfelder1871Verses { get; private set; } = new() ;

    public Elberfelder1871Strategy(IFileService fileService, IDbWriter writer, IDbReader reader,
        IOptions<AppSettings> options)
    {
        _fileService = fileService;
        _writer = writer;
        _reader = reader;
        _options = options.Value.DatabaseSeeding;
    }

    public async Task SaveToDatabase(string filePath, CancellationToken token = default)
    {
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

        await SaveToDatabaseAsync(Elberfelder1871Verses, 100, token);
    }

    private async Task SaveToDatabaseAsync(List<Elb1871Verse> list, int batchSize, CancellationToken token = default)
    {
        var batch = new List<Elb1871Verse>();

        foreach (var verse in list)
        {
            batch.Add(verse);

            if (batch.Count >= batchSize)
            {
                await WriteVersesToDbAsync(batch, token);
                await WriteWordsToDbAsync(batch, token);
                batch.Clear();
            }
        }

        await WriteVersesToDbAsync(batch, token);
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
            result = result.Trim(character);
        }

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
}
public record Elb1871Verse(int BibleBookId, int Chapter, int Verse, string Text, List<Elb1871Word> Words);
public record Elb1871Word(int Order, string InContext, string PlainWord);
