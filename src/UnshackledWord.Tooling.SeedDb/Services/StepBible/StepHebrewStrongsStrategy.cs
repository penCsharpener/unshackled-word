using System.Text;
using System.Text.RegularExpressions;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Abstractions.Step;
using UnshackledWord.Domain.Extensions;
using UnshackledWord.Domain.Models.Dbo.Step;
using UnshackledWord.Tooling.SeedDb.Services.Abstractions;
using UnshackledWord.Tooling.SeedDb.Services.StepBible.Models;

namespace UnshackledWord.Tooling.SeedDb.Services.StepBible;

public sealed partial class StepHebrewStrongsStrategy : IFileParserStrategy<List<StepHebrewStrongsEntry>>
{
    private readonly IFileService _fileService;
    private readonly IStepStrongsRepository _repo;
    private readonly ILogger<StepHebrewStrongsStrategy> _logger;
    private static readonly Regex _lineStart = IsLineStart();

    public StepHebrewStrongsStrategy(IFileService fileService, IStepStrongsRepository repo, ILogger<StepHebrewStrongsStrategy> logger)
    {
        _fileService = fileService;
        _repo = repo;
        _logger = logger;
    }

    public async Task<List<StepHebrewStrongsEntry>> SaveToDatabase(string filePath, CancellationToken token = default)
    {
        var filter = new StepStrongsFilter();
        var count = await _repo.CountByFilterAsync(filter, token);
        if (count > 0)
        {
            _logger.LogInformation("Step Hebrew strongs file data already imported... {count} rows", count);
            return [];
        }

        var lines = await _fileService.ReadAllLinesAsync(filePath, Encoding.UTF8, token);

        return ParseLines(lines);
    }

    /// <summary>Reads and parses the TBESH file without looking at the database (used by the lexicon repair).</summary>
    public async Task<List<StepHebrewStrongsEntry>> ParseFileAsync(string filePath, CancellationToken token = default)
    {
        var lines = await _fileService.ReadAllLinesAsync(filePath, Encoding.UTF8, token);

        return ParseLines(lines);
    }

    /// <summary>
    /// Parses the data lines of the TBESH file. The eStrong column (first) carries a lower-case letter for the
    /// disambiguated entries (<c>H5921a</c>, <c>H0834b</c>), so a data line is <c>H</c>, 4 or 5 digits, an optional lower-case letter and a tab.
    /// The number comes from the eStrong column, the extra (A, B, G, ...) from the dStrong column.
    /// </summary>
    public static List<StepHebrewStrongsEntry> ParseLines(IEnumerable<string> lines)
    {
        var parsedEntries = new List<StepHebrewStrongsEntry>();

        foreach (var line in lines)
        {
            if (!_lineStart.IsMatch(line))
            {
                continue;
            }

            var columns = line.Split('\t', StringSplitOptions.TrimEntries);
            var overrideLanguage = columns[5].StartsWith('A') ? StrongsLanguage.Aramaic : default(StrongsLanguage?);
            var extended = StrongsToText.StrongsRegexParser.Parse(columns[0], overrideLanguage).ToList().First();
            var disambiguated = StrongsToText.StrongsRegexParser.Parse(columns[1], overrideLanguage).ToList().First();
            var unified = StrongsToText.StrongsRegexParser.Parse(columns[2]).Distinct().ToList();

            var entry = new StepHebrewStrongsEntry
            {
                LanguageId = extended.LanguageId,
                Number = extended.Number,
                Extra = disambiguated.Extra,
                DisambiguatedExtra = disambiguated.DisambiguatedExtra,
                UnifiedEntries = unified.Select(x => new StepStrongsUnifiedEntry
                {
                    Extra = x.Extra,
                    LanguageId = x.LanguageId,
                    Number = x.Number
                }).ToList(),
                Hebrew = columns[3],
                Transliteration = columns[4],
                Morphology = columns[5],
                Gloss = columns[6],
                Meaning = columns[7],
            };

            entry.HebrewNoDiacritics = entry.Hebrew.RemoveHebrewDiacritics()!;

            parsedEntries.Add(entry);
        }

        return parsedEntries;
    }

    [GeneratedRegex("^H\\d{4,5}[a-z]?\t")]
    private static partial Regex IsLineStart();
}
