using CsvHelper.Configuration.Attributes;
using UnshackledWord.Domain.Models.BibleStructure;
using UnshackledWord.Domain.Models.Dbo.Step;

namespace UnshackledWord.Tooling.SeedDb.Services.StepBible.Models;

public class StepAmalgamatedHebrewEntry
{
    public int EntryId { get; set; }

    public BibleBook BibleBook { get; set; }

    public BibleReference BibleReference { get; set; }

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

    [Name("Hebrew")]
    public string Hebrew { get; set; } = default!;

    [Name("Hebrew No Diacritics")]
    public string HebrewNoDiacritics { get; set; } = default!;

    [Name("Hebrew Normalised")]
    public string HebrewNormalised { get; set; } = default!;

    [Name("Transliteration")]
    public string Transliteration { get; set; } = default!;

    [Name("English translation")]
    public string Gloss { get; set; } = default!;

    [Name("dStrongs")]
    public string DisambiguatedStrongs { get; set; } = default!;

    [Name("Grammar")]
    public string Grammar { get; set; } = default!;

    [Name("Meaning variants")]
    public string MeaningVariants { get; set; } = default!;

    [Name("Spelling variants")]
    public string SpellingVariants { get; set; } = default!;

    [Name("Root dStrongs+Instance")]
    public string RootDisambiguatedStrongsInstance { get; set; } = default!;

    [Name("Alternative Strongs+Instance")]
    public string AlternativeStrongs { get; set; } = default!;

    [Name("Conjoin word")]
    public string ConjoinWord { get; set; } = default!;

    [Name("Expanded Strong tags")]
    public string ExpandedStrongTags { get; set; } = default!;
}
