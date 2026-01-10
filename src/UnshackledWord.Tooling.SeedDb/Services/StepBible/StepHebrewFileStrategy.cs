using System.Text;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.Models.BibleStructure;
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

            entry.Hebrew = DenormalizeHebrew(entry.HebrewNormalised);
            entry.HebrewNoDiacritics = RemoveDiacritics(entry.Hebrew);

            parsedEntries.Add(entry);
        }

        return parsedEntries;
    }

    public string GetAtIndex(string[] columns, int index)
    {
        return columns.Length > index ? columns[index] : string.Empty;
    }

    private string DenormalizeHebrew(string input)
    {
        return input.Replace("/", "").Replace(@"\", "");
    }

    private string RemoveDiacritics(string input)
    {
        input = input.Replace("\u05B0", "") // Sheva;
            .Replace("\u05B1", "") // Hataf Segol
            .Replace("\u05B2", "") // Hataf Patah
            .Replace("\u05B3", "") // Hataf Qamats
            .Replace("\u05B4", "") // Hiriq
            .Replace("\u05B5", "") // Tsere
            .Replace("\u05B6", "") // Segol
            .Replace("\u05B7", "") // Patah
            .Replace("\u05B8", "") // Qamats
            .Replace("\u05B9", "") // Holam
            .Replace("\u05BA", "") // Holam Haser for Vav
            .Replace("\u05BB", "") // Qubuts
            .Replace("\u05BC", "") // Dagesh or Mapiq
            .Replace("\u05BD", "") // Meteg
            .Replace("\u05BE", "") // Maqqef
            .Replace("\u05BF", "") // Rafe
            .Replace("\u05C1", "") // Shin Dot
            .Replace("\u05C2", "") // Sin Dot
            .Replace("\u05C3", "") // Sof Pasuq
            .Replace("\u05C4", "") // Upper Dot
            .Replace("\u05C5", "") // Lower Dot
            .Replace("\u05C6", "") // Nun Hafukha
            .Replace("\u05C7", ""); // Qamats Qatan

        // remove cantillation marks
        for (var c = '\u0591'; c <= '\u05AF'; c++)
        {
            input = input.Replace(c.ToString(), "");
        }

        return input;
    }
}
