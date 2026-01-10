using System.Text;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Tooling.SeedDb.Services.Abstractions;
using UnshackledWord.Tooling.SeedDb.Services.StepBible.Models;

namespace UnshackledWord.Tooling.SeedDb.Services.StepBible;

public sealed class StepHebrewFileStrategy : IFileParserStrategy<List<StepAmalgamatedHebrewEntry>>
{
    private readonly IFileService _fileService;

    public StepHebrewFileStrategy(IFileService fileService)
    {
        _fileService = fileService;
    }

    public async Task<List<StepAmalgamatedHebrewEntry>> SaveToDatabase(string filePath, CancellationToken token = default)
    {
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

            var entry = new StepAmalgamatedHebrewEntry
            {
                BookAbbreviation = bibleReference.Book,
                Chapter = bibleReference.Chapter,
                Verse = bibleReference.Verse,
                AlternativeChapter = altBibleReference?.Chapter,
                AlternativeVerse = altBibleReference?.Verse,
                PositionInVerse = positionInVerse,
                Type = type,
                HebrewNormalised = GetAtIndex(columns, 1),
                Transliteration = GetAtIndex(columns, 2),
                Gloss = GetAtIndex(columns, 3),
                DisambiguatedStrongs = GetAtIndex(columns, 4),
                Grammar = GetAtIndex(columns, 5),
                MeaningVariants = GetAtIndex(columns, 6),
                SpellingVariants = GetAtIndex(columns, 7),
                RootDisambiguatedStrongsInstance = GetAtIndex(columns, 8),
                AlternativeStrongs = GetAtIndex(columns, 9),
                ConjoinWord = GetAtIndex(columns, 10),
                ExpandedStrongTags = GetAtIndex(columns, 11),
            };

            parsedEntries.Add(entry);
        }

        return parsedEntries;
    }

    public string GetAtIndex(string[] columns, int index)
    {
        return columns.Length > 1 ? columns[1] : string.Empty;
    }
}
