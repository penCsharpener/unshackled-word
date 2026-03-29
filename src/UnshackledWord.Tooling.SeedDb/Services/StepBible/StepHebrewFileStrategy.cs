using System.Text;
using System.Text.RegularExpressions;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Abstractions.Step;
using UnshackledWord.Domain.Extensions;
using UnshackledWord.Domain.Models.BibleStructure;
using UnshackledWord.Domain.Models.Dbo.Step;
using UnshackledWord.Tooling.SeedDb.Services.Abstractions;
using UnshackledWord.Tooling.SeedDb.Services.StepBible.Models;

namespace UnshackledWord.Tooling.SeedDb.Services.StepBible;

public sealed partial class StepHebrewFileStrategy : IFileParserStrategy<List<StepAmalgamatedHebrewEntry>>
{
    private readonly IFileService _fileService;
    private readonly IStepHebrewWordsRepository _repo;
    private readonly ILogger<StepHebrewFileStrategy> _logger;

    public StepHebrewFileStrategy(IFileService fileService, IStepHebrewWordsRepository repo, ILogger<StepHebrewFileStrategy> logger)
    {
        _fileService = fileService;
        _repo = repo;
        _logger = logger;
    }

    public async Task<List<StepAmalgamatedHebrewEntry>> SaveToDatabase(string filePath, CancellationToken token = default)
    {
        var filter = new StepHebrewWordFilter();
        var count = await _repo.CountByFilterAsync(filter, token);
        if (count > 0)
        {
            _logger.LogInformation("Step Hebrew file data already imported... {count} rows", count);
            return [];
        }

        var lines = await _fileService.ReadAllLinesAsync(filePath, Encoding.UTF8, token);
        var parsedEntries = new List<StepAmalgamatedHebrewEntry>();
        var canReadVerseData = false;

        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            var columns = line.Split('\t', StringSplitOptions.TrimEntries);
            // Implement parsing logic based on the STEP file format

            if (columns.Length == 0)
            {
                continue;
            }

            if (line.StartsWith("Eng (Heb) Ref & Type") && !canReadVerseData)
            {
                canReadVerseData = true;
                continue;
            }

            if (!columns[0].Contains('#') || !columns[0].Contains('=') || !columns[0].Contains('.') ||
                line.StartsWith('\t') || !canReadVerseData)
            {
                continue;
            }

            var refParts = columns[0].Split(['#', '='],
                StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            var bibleRef = refParts.Length > 0 ? refParts[0] : string.Empty;
            (string Book, int Chapter, int Verse) bibleReference;
            (string Book, int Chapter, int Verse)? altBibleReference;
            var bibleRefParts = bibleRef.Split(['.', '[', ']', '(', ')', '{', '}'],
                StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (bibleRef.Contains('[') || bibleRef.Contains('(') || bibleRef.Contains('{'))
            {
                var book = bibleRefParts.Length > 0 ? bibleRefParts[0] : string.Empty;
                var chapter = bibleRefParts.Length > 1 ? int.Parse(bibleRefParts[1]) : 0;
                var verse = bibleRefParts.Length > 2 ? int.Parse(bibleRefParts[2]) : 0;
                bibleReference = (book, chapter, verse);
                var altChapter = bibleRefParts.Length > 3 ? int.Parse(bibleRefParts[3]) : 0;
                var altVerse = bibleRefParts.Length > 4 ? int.Parse(bibleRefParts[4]) : 0;
                altBibleReference = (book, altChapter, altVerse);
            }
            else
            {
                var chapter = bibleRefParts.Length > 1 ? int.Parse(bibleRefParts[1]) : 0;
                var verse = bibleRefParts.Length > 2 ? int.Parse(bibleRefParts[2]) : 0;
                var book = bibleRefParts.Length > 0 ? bibleRefParts[0] : string.Empty;
                bibleReference = (book, chapter, verse);
                altBibleReference = null;
            }

            var positionInVerse = refParts.Length > 1 ? int.Parse(refParts[1]) : 0;
            var type = refParts.Length > 2 ? refParts[2] : string.Empty;
            var bibleBook = BibleBook.FindByAbbreviation(bibleReference.Book)!.Value;

            var entry = new StepAmalgamatedHebrewEntry
            {
                BibleBook = bibleBook,
                BibleReference = new BibleReference(bibleBook.Id, bibleReference.Chapter, bibleReference.Verse),
                BookAbbreviation = bibleReference.Book,
                Chapter = bibleReference.Chapter,
                Verse = bibleReference.Verse,
                AlternativeChapter = altBibleReference?.Chapter,
                AlternativeVerse = altBibleReference?.Verse,
                PositionInVerse = positionInVerse,
                Type = type,
                HebrewNormalised = GetAtIndex(columns, 1, string.Empty)!,
                Transliteration = GetAtIndex(columns, 2, string.Empty)!,
                Gloss = GetAtIndex(columns, 3, string.Empty)!,
                DisambiguatedStrongs = GetAtIndex(columns, 4, string.Empty)!,
                Grammar = GetAtIndex(columns, 5, string.Empty)!,
                MeaningVariants = GetAtIndex(columns, 6)!,
                SpellingVariants = GetAtIndex(columns, 7)!,
                RootDisambiguatedStrongsInstance = GetAtIndex(columns, 8)!,
                AlternativeStrongs = GetAtIndex(columns, 9)!,
                ConjoinWord = GetAtIndex(columns, 10)!,
                ExpandedStrongTags = GetAtIndex(columns, 11)!,
            };

            var internalStrongs = StrongsRegexParser.Parse(entry.DisambiguatedStrongs).ToList();
            entry.StrongsNumbers = internalStrongs.ToDbo(null, null).ToList();

            entry.Hebrew = DenormalizeHebrew(entry.HebrewNormalised);
            entry.HebrewNoDiacritics = entry.Hebrew.RemoveHebrewDiacritics()!;

            parsedEntries.Add(entry);
        }

        return parsedEntries;
    }

    public string? GetAtIndex(string[] columns, int index, string? defaultValue = null)
    {
        if (columns.Length > index)
        {
            if (columns[index].IsNullOrEmpty())
            {
                return defaultValue;
            }

            return columns[index];
        }

        return defaultValue;
    }

    private string DenormalizeHebrew(string? input)
    {
        if (input.IsNullOrEmpty())
        {
            return string.Empty;
        }

        return input.Replace("/", "").Replace(@"\", "");
    }
}
