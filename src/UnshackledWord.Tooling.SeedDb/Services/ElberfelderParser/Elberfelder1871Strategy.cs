using System.Text;
using UnshackledWord.Application.Abstractions;
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
        _countWords = await GetCountWordsAsync(token);

        if (_countVerses > 0 || _countWords > 0)
        {
            _logger.LogInformation("Elberfelder 1871 verses and words already exist in the database. Skipping import. " +
                                   "{countVerses} rows of verses {countWords} rows of words ", _countVerses, _countWords);
            return;
        }

        var totalWords = new List<Elb1871WordDbo>();
        var lines = await _fileService.ReadAllLinesAsync(filePath, Encoding.UTF8, token);
        var verse = "";
        var id = 1;

        for (var i = 0; i < lines.Length; i++)
        {
            var lineItem = new ElbExportLineItem(lines[i]);
            var wordDbos = new List<Elb1871WordDbo>(120);

            if (i < lines.Length - 1)
            {
                var nextLineItem = new ElbExportLineItem(lines[i + 1]);

                wordDbos = lineItem.Words.Select(x =>
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

                if (lineItem.HebRefId.RefId == nextLineItem.HebRefId.RefId)
                {
                    var nextWordDtos = nextLineItem.Words.Select(x =>
                    {
                        var word = new Elb1871WordDbo
                        {
                            Id = id,
                            BibleBookId = nextLineItem.HebRefId.BookId,
                            Chapter = nextLineItem.HebRefId.Chapter,
                            Verse = nextLineItem.HebRefId.Verse,
                            HebRefId = nextLineItem.HebRefId.RefId,
                            WordInContext = x.InContext,
                            PlainWord = x.PlainWord,
                            PositionInVerse = x.Order + lineItem.Words.Count
                        };
                        id++;

                        return word;
                    }).ToList();
                    wordDbos.AddRange(nextWordDtos);
                    i++;
                }
            }

            totalWords.AddRange(wordDbos);
            wordDbos.Clear();
        }

        _logger.LogInformation("Saving split words to Database.");
        await BulkInsertIntoDatabaseAsync(totalWords, token);
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

        await _writer.WriteAsync(sql, parameters);
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
