using System.Text;
using Microsoft.Extensions.Options;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.Extensions;
using UnshackledWord.Domain.Models.BibleStructure;
using UnshackledWord.Domain.Models.Dbo;
using UnshackledWord.Domain.Models.Settings;
using UnshackledWord.Tooling.SeedDb.Services.Abstractions;

namespace UnshackledWord.Tooling.SeedDb.Services.SBL;

public class SblGntApparatusStrategy : IFileParserStrategy
{
    private readonly IFileService _fileService;
    private readonly IDbWriter _dbWriter;
    private readonly SblSettings _options;
    private static string _delimiter = $",{Environment.NewLine}    ";

    public SblGntApparatusStrategy(IFileService fileService, IDbWriter dbWriter, IOptions<AppSettings> options)
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
        var apparatusItems = new List<ApparatusEntry>();

        foreach (var fileName in Constants.SblDownloadFileNames.Keys)
        {
            var filePath = _fileService.Combine(_options.ApparatusFilePath, $"{fileName}.txt");
            var lines = await _fileService.ReadAllLinesAsync(filePath, Encoding.UTF8, token);
            var book = BibleBook.AllBooks[Constants.SblDownloadFileNames[fileName]];

            var apparatusEntry = new ApparatusEntry();
            var sb = new StringBuilder();
            var isEmptyLine = false;
            var isReferenceLine = false;
            var isBulletLine = false;
            var isFirstApparatusLine = false;

            foreach (var line in lines[1..])
            {
                if (line.IsNullOrWhiteSpace())
                {
                    isEmptyLine = true;
                    isFirstApparatusLine = false;
                    isBulletLine = false;

                    if (sb.Length > 0)
                    {
                        apparatusEntry.Text = sb.ToString().Trim(' ').Replace(" ", " ");
                        apparatusItems.Add(apparatusEntry);
                        apparatusEntry = new ApparatusEntry();
                        sb.Clear();
                        continue;
                    }

                    if (sb.Length == 0)
                    {
                        continue;
                    }
                }

                if (line.StartsWith(book.Name) && line.Contains(':'))
                {
                    isEmptyLine = false;
                    isReferenceLine = true;
                    apparatusEntry.BibleBookId = book.Id;
                    var parts = line.Replace(book.Name, "").Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    var chapterVerse = parts[0].Split(':');
                    apparatusEntry.Chapter = int.Parse(chapterVerse[0]);
                    apparatusEntry.Verse = int.Parse(chapterVerse[1]);

                    continue;
                }

                isReferenceLine = false;

                var mutatedLine = line;
                if (line.StartsWith($"{apparatusEntry.Chapter}:{apparatusEntry.Verse} "))
                {
                    isFirstApparatusLine = true;
                    var startSize = $"{apparatusEntry.Chapter}:{apparatusEntry.Verse} ".Length;
                    mutatedLine = line[startSize..];
                } else if (mutatedLine.StartsWith($"{apparatusEntry.Verse} "))
                {
                    isFirstApparatusLine = true;
                    var startSize = $"{apparatusEntry.Verse} ".Length;
                    mutatedLine = line[startSize..];
                } else if (mutatedLine.StartsWith("• "))
                {
                    isFirstApparatusLine = false;
                    isBulletLine = true;
                }

                sb.AppendLine(mutatedLine);
            }
        }

        var insertSql = $"""
                         INSERT INTO {SblApparatusDbo.DboName} ("BibleBookId", "Chapter", "Verse", "RefId", "Text")
                         VALUES
                         {apparatusItems.Select(x => x.ToString()).JoinStrings(_delimiter)};
                         """;

        await _dbWriter.WriteAsync(insertSql);
    }

    private record struct ApparatusEntry()
    {
        public int BibleBookId { get; set; }
        public int Chapter { get; set; }
        public int Verse { get; set; }
        public int RefId { get; set; }
        public string Text { get; set; }
        public BibleReference BibRef => new(BibleBookId, Chapter, Verse);

        public override string ToString() => $"({BibleBookId}, {Chapter}, {Verse}, {BibRef.RefId}, '{Text}')";
    }
}
