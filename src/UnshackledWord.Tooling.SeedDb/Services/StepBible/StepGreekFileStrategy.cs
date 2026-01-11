using System.Globalization;
using System.Text;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Abstractions.Step;
using UnshackledWord.Domain.Extensions;
using UnshackledWord.Domain.Models.BibleStructure;
using UnshackledWord.Tooling.SeedDb.Services.Abstractions;
using UnshackledWord.Tooling.SeedDb.Services.StepBible.Models;

namespace UnshackledWord.Tooling.SeedDb.Services.StepBible;

public sealed class StepGreekFileStrategy : IFileParserStrategy<List<StepAmalgamatedGreekEntry>>
{
    private readonly IFileService _fileService;
    private readonly IStepGreekWordsRepository _repo;

    public StepGreekFileStrategy(IFileService fileService, IStepGreekWordsRepository repo)
    {
        _fileService = fileService;
        _repo = repo;
    }

    public async Task<List<StepAmalgamatedGreekEntry>> SaveToDatabase(string filePath, CancellationToken token = default)
    {
        var filter = new StepGreekWordFilter { IncludedBibleBookIds = [40]};
        var count = await _repo.CountByFilterAsync(filter, token);
        if (count > 0)
        {
            return [];
        }

        var lines = await _fileService.ReadAllLinesAsync(filePath, Encoding.UTF8, token);
        var parsedEntries = new List<StepAmalgamatedGreekEntry>();

        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            var columns = line.Split('\t', StringSplitOptions.TrimEntries);

            if (columns.Length == 0)
            {
                continue;
            }

            if (!columns[0].Contains('#') || !columns[0].Contains('=') || !columns[0].Contains('.') ||
                line.StartsWith('\t'))
            {
                continue;
            }

            var refParts = columns[0].Split(['#', '='], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            var bibleRef = GetAtIndex(refParts, 0);
            (string Book, int Chapter, int Verse) bibleReference;
            (string Book, int Chapter, int Verse)? altBibleReference;
            var bibleRefParts = bibleRef.Split(['.', '[', ']', '(', ')', '{', '}'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (bibleRef.Contains('[') || bibleRef.Contains('(') || bibleRef.Contains('{'))
            {
                var book = GetAtIndex(bibleRefParts, 0);
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
                var book = GetAtIndex(bibleRefParts, 0);
                bibleReference = (book, chapter, verse);
                altBibleReference = null;
            }
            var positionInVerse = refParts.Length > 1 ? int.Parse(refParts[1]) : 0;
            var type = GetAtIndex(refParts, 2);

            var grWithTransliteration = GetAtIndex(columns, 1);
            var grParts = grWithTransliteration.Split(['(', ')'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            var gram = GetAtIndex(columns, 3);
            var gramParts = gram.Split("=", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            var form = GetAtIndex(columns, 4);
            var formParts = form.Split("=", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            var bibleBook = BibleBook.FindByAbbreviation(bibleReference.Book)!.Value;

            var entry = new StepAmalgamatedGreekEntry
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
                FoundInNestleAland = type.Contains('N'),
                FoundInTextusReceptus = type.Contains('K'),
                FoundInOther = type.Contains('O'),
                Greek = GetAtIndex(grParts, 0),
                Transliteration = GetAtIndex(grParts, 1),
                EnglishTranslation = GetAtIndex(columns, 2),
                DisambiguatedStrongs = GetAtIndex(gramParts, 0),
                Morphology = GetAtIndex(gramParts, 1),
                Lemma = GetAtIndex(formParts, 0),
                Gloss = GetAtIndex(formParts, 1),
                Editions = GetAtIndex(columns, 5),
                //EditionList = GetAtIndex(columns, 5).Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries),
                MeaningVariants = GetAtIndex(columns, 6),
                SpellingVariants = GetAtIndex(columns, 7),
                SpanishTranslation = GetAtIndex(columns, 8),
                SubMeaning = GetAtIndex(columns, 9),
                ConjoinWord = GetAtIndex(columns, 10),
                StrongInstance = GetAtIndex(columns, 11),
                AltStrongs = GetAtIndex(columns, 12)
            };

            entry.GreekNoDiacritics = entry.Greek.RemoveGreekDiacritics()!;
            entry.LemmaNoDiacritics = entry.Lemma.RemoveGreekDiacritics()!;

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
}
