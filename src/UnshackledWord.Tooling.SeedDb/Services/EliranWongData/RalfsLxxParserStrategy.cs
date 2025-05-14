using System.Text;
using Microsoft.Extensions.Options;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.Models.Settings;
using UnshackledWord.Tooling.SeedDb.Services.Abstractions;

namespace UnshackledWord.Tooling.SeedDb.Services.EliranWongData;

public class RalfsLxxParserStrategy : IFileParserStrategy
{
    private readonly IFileService _fileService;
    private readonly DatabaseSeedSettings _options;

    public RalfsLxxParserStrategy(IFileService fileService, IOptions<AppSettings> options)
    {
        _fileService = fileService;
        _options = options.Value.DatabaseSeeding;
    }

    public async Task SaveToDatabase(string filePath, CancellationToken token = default)
    {
        if (_fileService.DirectoryExists(_options.RepoLocationEliranWongLxxRalfs1935) is false)
        {
            return;
        }

        var repoPath = @"01_wordlist_unicode\alignment_with_OSSP";
        var lxxWordFilePath = _fileService.Combine(_options.RepoLocationEliranWongLxxRalfs1935, repoPath, "E-text.csv");
        var lxxWordListLines = await _fileService.ReadAllLinesAsync(lxxWordFilePath, Encoding.UTF8, token);
        var lxxVerseFilePath =
            _fileService.Combine(_options.RepoLocationEliranWongLxxRalfs1935, repoPath, "E-verse.csv");
        var lxxVerseList = await _fileService.ReadAllLinesAsync(lxxVerseFilePath, Encoding.UTF8, token);

        var dictionaryWords = new Dictionary<int, LxxWords>();

        foreach (var line in lxxWordListLines)
        {
            var splitColumns = line.Split('\t');

            if (splitColumns.Length == 2)
            {
                dictionaryWords.Add(int.Parse(splitColumns[0]),
                    new LxxWords(int.Parse(splitColumns[0]), 0, splitColumns[1]));
                continue;
            }

            dictionaryWords.Add(int.Parse(splitColumns[0]),
                new LxxWords(int.Parse(splitColumns[0]), int.Parse(splitColumns[1]), splitColumns[2]));
        }

        var dictionaryVerses = new Dictionary<int, LxxVerse>();

        var lineIdx = 0;
        foreach (var line in lxxVerseList)
        {
            var splitColumns = line.Split('\t');
            var splitColumnsNextVerse = lineIdx == lxxVerseList.Length - 1
                ? ["", dictionaryWords.Count.ToString(), ""]
                : lxxVerseList[lineIdx + 1].Split('\t');
            var startId = int.Parse(splitColumns[0]);
            var endId = int.Parse(splitColumns[1]);
            var nextVerseEndId = int.Parse(splitColumnsNextVerse[1]);
            var reference = splitColumns[2].Replace("「", "").Replace("」", "");

            dictionaryVerses.Add(startId, new LxxVerse(startId, endId, reference, new List<LxxWords>()));

            for (int i = startId; i < nextVerseEndId; i++)
            {
                if (dictionaryWords.TryGetValue(i, out var lxxWord))
                {
                    dictionaryVerses[startId].Verses.Add(lxxWord);
                }
            }

            lineIdx++;
        }
    }
}

public record struct LxxWords(int EliranId, int OsspId, string greekWord);

public record struct LxxVerse(int StartId, int EndId, string Reference, List<LxxWords> Verses);
