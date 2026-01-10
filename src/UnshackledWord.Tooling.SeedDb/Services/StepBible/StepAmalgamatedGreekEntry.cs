using CsvHelper.Configuration.Attributes;

namespace UnshackledWord.Tooling.SeedDb.Services.StepBible;

public class StepAmalgamatedGreekEntry
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

    [Name("Transliteration")]
    public string Transliteration { get; set; } = default!;

    [Name("English translation")]
    public string EnglishTranslation { get; set; } = default!;

    [Name("dStrongs")]
    public string DisambiguatedStrongs { get; set; } = default!;

    [Name("Grammar")]
    public string Grammar { get; set; } = default!;

    [Name("Gloss")]
    public string Gloss { get; set; } = default!;

    [Name("Dictionary form")]
    public string Lemma { get; set; } = default!;

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
