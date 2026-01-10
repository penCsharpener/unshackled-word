using System.Text;
using CsvHelper.Configuration.Attributes;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Tooling.SeedDb.Services.Abstractions;

namespace UnshackledWord.Tooling.SeedDb.Services.StepBible;

public sealed class StepFileStrategy : IFileParserStrategy
{
    private readonly IFileService _fileService;

    public StepFileStrategy(IFileService fileService)
    {
        _fileService = fileService;
    }

    public async Task SaveToDatabase(string filePath, CancellationToken token = default)
    {
        var lines = await _fileService.ReadAllLinesAsync(filePath, Encoding.UTF8, token);
        var parsedEntries = new List<StepAmalgamatedEntry>();

        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            var columns = line.Split('\t', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            // Implement parsing logic based on the STEP file format

            if (columns.Length == 0)
            {
                continue;
            }

            if (columns[0].Contains('#') && columns[0].Contains('=') && columns[0].Contains('.'))
            {
                // Act.2.11[2.10] #01=NKO
                var refParts = columns[0].Split(['#', '='], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                var book = refParts.Length > 0 ? refParts[0] : string.Empty;
                var chapter = refParts.Length > 1 ? int.Parse(refParts[1]) : 0;
                var verse = refParts.Length > 2 ? int.Parse(refParts[2]) : 0;
                var positionInVerse = refParts.Length > 3 ? int.Parse(refParts[3]) : 0;
                var type = refParts.Length > 4 ? refParts[4] : string.Empty;
                var grWithTransliteration = columns.Length > 1 ? columns[1] : string.Empty;
                var grParts = grWithTransliteration.Split(" (", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                var gram = columns.Length > 3 ? columns[3] : string.Empty;
                var gramParts = gram.Split("=", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                var form = columns.Length > 4 ? columns[4] : string.Empty;
                var formParts = form.Split("=", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

                var entry = new StepAmalgamatedEntry
                {
                    BookAbbreviation = book,
                    Chapter = chapter,
                    Verse = verse,
                    PositionInVerse = positionInVerse,
                    Type = type,
                    Greek = grParts.Length > 0 ? grParts[0] : string.Empty,
                    GreekTransliteration = grParts.Length > 1 ? columns[1].Replace(")", "") : string.Empty,
                    EnglishTranslation = columns.Length > 2 ? columns[2] : string.Empty,
                    DisambiguatedStrongs = gramParts.Length > 0 ? gramParts[0] : string.Empty,
                    Grammar = gramParts.Length > 1 ? gramParts[1] : string.Empty,
                    DictionaryForm = formParts.Length > 0 ? formParts[0] : string.Empty,
                    Gloss = formParts.Length > 1 ? formParts[1] : string.Empty,
                    Editions = columns.Length > 5 ? columns[5] : string.Empty,
                    MeaningVariants = columns.Length > 6 ? columns[6] : string.Empty,
                    SpellingVariants = columns.Length > 7 ? columns[7] : string.Empty,
                    SpanishTranslation = columns.Length > 8 ? columns[8] : string.Empty,
                    SubMeaning = columns.Length > 9 ? columns[9] : string.Empty,
                    ConjoinWord = columns.Length > 10 ? columns[10] : string.Empty,
                    StrongInstance = columns.Length > 11 ? columns[11] : string.Empty,
                    AltStrongs = columns.Length > 12 ? columns[12] : string.Empty
                };

                parsedEntries.Add(entry);
                // Here you would typically save the entry to the database
                // For example: await _databaseService.SaveBiblicalLexiconEntryAsync(entry, token);
            }
        }


    }
}

public class StepAmalgamatedEntry
{
    [Name("Book")]
    public string BookAbbreviation { get; set; } = default!;

    [Name("Chapter")]
    public int Chapter { get; set; }

    [Name("Verse")]
    public int Verse { get; set; }

    [Name("Alternative Chapter")]
    public int? AlternativeChapter { get; set; }

    [Name("Alternative Verse")]
    public int? AlternativeVerse { get; set; }

    [Name("Position In Verse")]
    public int PositionInVerse { get; set; }

    [Name("Type")]
    public string Type { get; set; } = default!;

    [Name("Greek")]
    public string Greek { get; set; } = default!;

    [Name("Greek Transliteration")]
    public string GreekTransliteration { get; set; } = default!;

    [Name("English translation")]
    public string EnglishTranslation { get; set; } = default!;

    [Name("dStrongs")]
    public string DisambiguatedStrongs { get; set; } = default!;

    [Name("Grammar")]
    public string Grammar { get; set; } = default!;

    [Name("Gloss")]
    public string Gloss { get; set; } = default!;

    [Name("Dictionary form")]
    public string DictionaryForm { get; set; } = default!;

    [Name("editions")]
    public string Editions { get; set; } = default!;

    [Name("Meaning variants")]
    public string MeaningVariants { get; set; } = default!;

    [Name("Spelling variants")]
    public string SpellingVariants { get; set; } = default!;

    [Name("Spanish translation")]
    public string SpanishTranslation { get; set; } = default!;

    [Name("Sub-meaning")]
    public string SubMeaning { get; set; } = default!;

    [Name("Conjoin word")]
    public string ConjoinWord { get; set; } = default!;

    [Name("sStrong+Instance")]
    public string StrongInstance { get; set; } = default!;

    [Name("Alt Strongs")]
    public string AltStrongs { get; set; } = default!;
}
