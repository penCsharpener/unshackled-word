using UnshackledWord.Infrastructure.Seed;
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

        if (_countWords > 0)
        {
            _logger.LogInformation("Elberfelder 1871 words already exist in the database. Skipping import. " +
                                   "{countWords} rows of words ", _countWords);
            return;
        }

        var lines = await _fileService.ReadAllLinesAsync(filePath, Encoding.UTF8, token);
        var totalWords = Elb1871WordParser.Parse(lines);

        _logger.LogInformation("Saving split words to Database.");
        await BulkInsertIntoDatabaseAsync(totalWords, token);
    }

    private async Task BulkInsertIntoDatabaseAsync(List<Elb1871WordDbo> batch, CancellationToken token = default)
    {
        var sql = $"""
                   INSERT INTO {Elb1871WordDbo.DboName} ("Id", "BibleBookId", "Chapter", "Verse", "HebRefId", "WordInContext", "PositionInVerse", "PlainWord")
                   SELECT *
                   FROM UNNEST(@Id, @BibleBookId, @Chapter, @Verse, @HebRefId, @WordsInContext, @PositionInVerse, @PlainWord)
                   """;

        var parameters = new
        {
            Id = batch.Select(x => x.Id).ToArray(),
            BibleBookId = batch.Select(x => x.BibleBookId).ToArray(),
            Chapter = batch.Select(x => x.Chapter).ToArray(),
            Verse = batch.Select(x => x.Verse).ToArray(),
            HebRefId = batch.Select(x => x.HebRefId).ToArray(),
            WordsInContext = batch.Select(x => x.WordInContext).ToArray(),
            PositionInVerse = batch.Select(x => x.PositionInVerse).ToArray(),
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
