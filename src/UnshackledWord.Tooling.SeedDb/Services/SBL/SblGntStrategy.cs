using System.Text;
using Microsoft.Extensions.Options;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.Extensions;
using UnshackledWord.Domain.Models.BibleStructure;
using UnshackledWord.Domain.Models.Settings;
using UnshackledWord.Tooling.SeedDb.Services.Abstractions;

namespace UnshackledWord.Tooling.SeedDb.Services.SBL;

public class SblGntStrategy : IFileParserStrategy
{
    private readonly IFileService _fileService;
    private readonly IDbWriter _dbWriter;
    private readonly SblSettings _options;
    private static string _delimiter = $",{Environment.NewLine}    ";

    public SblGntStrategy(IFileService fileService, IDbWriter dbWriter, IOptions<AppSettings> options)
    {
        _fileService = fileService;
        _dbWriter = dbWriter;
        _options = options.Value.DatabaseSeeding.SblSettings;
    }

    public async Task SaveToDatabase(string _, CancellationToken token = default)
    {
        await GetCombinedLinesOfAllFilesAsync(token);
    }

    private async Task GetCombinedLinesOfAllFilesAsync(CancellationToken token = default)
    {
        var combinedLines = new List<string>();

        foreach (var fileName in Constants.SblDownloadFileNames)
        {
            var filePath = _fileService.Combine(_options.TextFilePath, fileName);
            var lines = await _fileService.ReadAllLinesAsync(filePath, Encoding.UTF8, token);
            var insertRows = new List<string>();

            foreach (var line in lines[1..])
            {
                var parts = line.Split('\t', StringSplitOptions.RemoveEmptyEntries);
                var refString = parts[0];
                var bookChapterVerse = refString.Split(' ');
                var bookString = bookChapterVerse[0];
                var bookId = BibleBook.AllBooks.First(x => x.Value.Abbreviations.Any(y => y == bookString)).Key;

                var chapterVerse = bookChapterVerse[1].Split(':');
                var chapter = int.Parse(chapterVerse[0]);
                var verse = int.Parse(chapterVerse[1]);

                var text = parts[1];

                insertRows.Add($"({bookId}, {chapter}, {verse}, '{text}')");
            }

            var insertSql = $"""
                            INSERT INTO "unshackled-word"."SblText" ("BibleBookId", "Chapter", "Verse", "VerseText")
                            VALUES
                            {insertRows.JoinStrings(_delimiter)};
                            """;

            await _dbWriter.WriteAsync(insertSql);
        }
    }
}
