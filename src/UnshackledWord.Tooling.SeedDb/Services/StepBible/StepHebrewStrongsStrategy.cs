using System.Text;
using System.Text.RegularExpressions;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Tooling.SeedDb.Services.Abstractions;
using UnshackledWord.Tooling.SeedDb.Services.StepBible.Models;

namespace UnshackledWord.Tooling.SeedDb.Services.StepBible;

public sealed partial class StepHebrewStrongsStrategy : IFileParserStrategy<List<StepHebrewStrongsEntry>>
{
    private readonly IFileService _fileService;
    private static Regex _lineStart = IsLineStart();

    public StepHebrewStrongsStrategy(IFileService fileService)
    {
        _fileService = fileService;
    }

    public async Task<List<StepHebrewStrongsEntry>> SaveToDatabase(string filePath, CancellationToken token = default)
    {
        var lines = await _fileService.ReadAllLinesAsync(filePath, Encoding.UTF8, token);
        var parsedEntries = new List<StepHebrewStrongsEntry>();

        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            var columns = line.Split('\t', StringSplitOptions.TrimEntries);
            var isDataLine = false;

            if (columns.Length == 0)
            {
                continue;
            }

            if (_lineStart.IsMatch(line) && !isDataLine)
            {
                isDataLine = true;
            }

            if (isDataLine)
            {
                var entry = new StepHebrewStrongsEntry()
                {
                    ExtendedStrongs = columns[0],
                    DisambiguatedStrongs = columns[1],
                    UnifiedStrongs = columns[2],
                    Hebrew = columns[3],
                    Transliteration = columns[4],
                    Morphology = columns[5],
                    Gloss = columns[6],
                    Meaning = columns[7],
                };

                parsedEntries.Add(entry);
            }
        }

        return parsedEntries;
    }

    [GeneratedRegex("^H\\d{4,5}\t")]
    private static partial Regex IsLineStart();
}
