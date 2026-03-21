using System.Text;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.Extensions;
using UnshackledWord.Domain.Models.BibleStructure;
using UnshackledWord.Domain.Models.Dbo;
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

        var totalWords = new List<Elb1871WordDbo>();
        var lines = await _fileService.ReadAllLinesAsync(filePath, Encoding.UTF8, token);
        var id = 1;

        foreach (var line in lines)
        {
            var refText = line.Split("||", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var hebRef = ParseBibleReference(refText[0]);
            var lxxRef = ParseBibleReference(refText[1]);

            if (refText.Length == 2 || (refText.Length == 2 && refText[2].IsNullOrWhiteSpace()))
            {
                continue;
            }

            var words = SplitAndSaveIndividualWords(refText[1]).ToList();
            var wordDbos = words.Select(x =>
            {
                var word = new Elb1871WordDbo
                {
                    Id = id,
                    BibleBookId = hebRef.BookId,
                    Chapter = hebRef.Chapter,
                    Verse = hebRef.Verse,
                    HebRefId = hebRef.RefId,
                    WordInContext = x.InContext,
                    PlainWord = x.PlainWord,
                    PositionInVerse = x.Order
                };
                id++;

                return word;
            }).ToList();
            totalWords.AddRange(wordDbos);
        }

        _logger.LogInformation("Saving split words to Database.");
        await BulkInsertIntoDatabaseAsync(totalWords, token);
    }

    private BibleReference ParseBibleReference(string stringReference)
    {
        var bookRef = stringReference.Split("$");
        var chapterVerse = bookRef[1].Split(":");

        var book = bookRef[0];
        var chapter = int.Parse(chapterVerse[0]);
        var verse = int.Parse(chapterVerse[1]);
        var bookId = BibleBook.AllBooks.First(x => x.Value.Name == book).Key;
        return new BibleReference(bookId, chapter, verse);
    }

    private async Task BulkInsertIntoDatabaseAsync(List<Elb1871WordDbo> batch, CancellationToken token = default)
    {
        var sql = $"""
                   INSERT INTO {Elb1871WordDbo.DboName} ("Id", "BibleBookId", "Chapter", "Verse", "HebRefId", "WordInContext", "PositionInVerse", "PlainWord")
                   SELECT *
                   FROM UNNEST(@Ids, @BookIds, @Chapters, @Verses, @HebRefIds, @WordsInContext, @PositionInVerses, @PlainWord)
                   """;

        var parameters = new
        {
            Ids = batch.Select(x => x.Id).ToArray(),
            BookIds = batch.Select(x => x.BibleBookId).ToArray(),
            Chapters = batch.Select(x => x.Chapter).ToArray(),
            Verses = batch.Select(x => x.Verse).ToArray(),
            HebRefIds = batch.Select(x => x.HebRefId).ToArray(),
            WordsInContext = batch.Select(x => x.WordInContext).ToArray(),
            PositionInVerses = batch.Select(x => x.PositionInVerse).ToArray(),
            PlainWord = batch.Select(x => x.PlainWord).ToArray()
        };

        var sqlHebLxxMapping = $"""
                                INSERT INTO {} ()
                                SELECT *
                                FROM UNNEST()
                                """;

        await _writer.WriteAsync(sql, parameters);
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

            yield return new Elb1871Word(new BibleReference(), orderCounter, word, cleanedWord);
            orderCounter++;
        }
    }

    private async Task<int> GetCountVersesAsync(CancellationToken token = default)
    {
        var sql = $"""
                   select Count(*)
                   from {Elb1871VersesDbo.DboName}
                   """;

        return await _reader.ExecuteScalarAsync<int>(sql);
    }

    private async Task<int> GetCountWordsAsync(CancellationToken token = default)
    {
        var sql = $"""
                   select Count(*)
                   from {Elb1871WordDbo.DboName}
                   """;

        return await _reader.ExecuteScalarAsync<int>(sql);
    }
}
public record Elb1871Verse(BibleReference BibRef, string Text, List<Elb1871Word> Words);
public record Elb1871Word(BibleReference BibRef, int Order, string InContext, string PlainWord);
