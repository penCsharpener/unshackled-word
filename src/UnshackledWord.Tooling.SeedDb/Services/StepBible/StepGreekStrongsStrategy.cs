using System.Text;
using System.Text.RegularExpressions;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Abstractions.Step;
using UnshackledWord.Domain.Extensions;
using UnshackledWord.Tooling.SeedDb.Services.Abstractions;
using UnshackledWord.Tooling.SeedDb.Services.StepBible.Models;

namespace UnshackledWord.Tooling.SeedDb.Services.StepBible;

public sealed partial class StepGreekStrongsStrategy : IFileParserStrategy<List<StepGreekStrongsEntry>>
{
    private readonly IFileService _fileService;
    private readonly IStepStrongsRepository _repo;
    private readonly ILogger<StepGreekStrongsStrategy> _logger;
    private static Regex _lineStart = IsLineStart();

    public StepGreekStrongsStrategy(IFileService fileService, IStepStrongsRepository repo, ILogger<StepGreekStrongsStrategy> logger)
    {
        _fileService = fileService;
        _repo = repo;
        _logger = logger;
    }

    public async Task<List<StepGreekStrongsEntry>> SaveToDatabase(string filePath, CancellationToken token = default)
    {
        var filter = new StepStrongsFilter();
        var count = await _repo.CountByFilterAsync(filter, token);
        if (count > 0)
        {
            _logger.LogInformation("Step Greek strongs file data already imported... {count} rows", count);
            return [];
        }

        var lines = await _fileService.ReadAllLinesAsync(filePath, Encoding.UTF8, token);
        var parsedEntries = new List<StepGreekStrongsEntry>();

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
                var entry = new StepGreekStrongsEntry
                {
                    ExtendedStrongs = columns[0],
                    DisambiguatedStrongs = columns[1],
                    UnifiedStrongs = columns[2],
                    Greek = columns[3],
                    Transliteration = columns[4],
                    Morphology = columns[5],
                    Gloss = columns[6],
                    AbbottSmithLexicon = columns[7],
                };

                entry.GreekNoDiacritics = entry.Greek.RemoveGreekDiacritics()!;

                parsedEntries.Add(entry);
            }
        }

        return parsedEntries;
    }

    [GeneratedRegex("^G\\d{4,5}\t")]
    private static partial Regex IsLineStart();
}
