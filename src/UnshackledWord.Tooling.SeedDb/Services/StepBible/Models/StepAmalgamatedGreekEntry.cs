using CsvHelper.Configuration.Attributes;
using UnshackledWord.Domain.Models.BibleStructure;
using UnshackledWord.Domain.Models.Dbo.Step;

namespace UnshackledWord.Tooling.SeedDb.Services.StepBible.Models;

public class StepAmalgamatedGreekEntry
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

    public int LxxRefId { get; set; }

    [Name("Alternative Chapter")]
    public int? AlternativeChapter { get; set; }

    [Name("Alternative Verse")]
    public int? AlternativeVerse { get; set; }

    [Name("Position In Verse")]
    public int PositionInVerse { get; set; }

    [Name("Type")]
    public string Type { get; set; } = default!;

    public bool FoundInNestleAland { get; set; }
    public bool FoundInTextusReceptus { get; set; }
    public bool FoundInOther { get; set; }

    [Name("Greek")]
    public string Greek { get; set; } = default!;

    public string GreekNoDiacritics { get; set; } = default!;

    [Name("Transliteration")]
    public string Transliteration { get; set; } = default!;

    [Name("English translation")]
    public string EnglishTranslation { get; set; } = default!;

    [Name("dStrongs")]
    public string DisambiguatedStrongs { get; set; } = default!;

    [Name("Grammar")]
    public string Morphology { get; set; } = default!;

    [Name("Gloss")]
    public string Gloss { get; set; } = default!;

    [Name("Dictionary form")]
    public string Lemma { get; set; } = default!;
    public string LemmaNoDiacritics { get; set; } = default!;

    [Name("editions")]
    public string Editions { get; set; } = default!;

    public string[] EditionList { get; set; } = default!;

    [Name("Meaning variants")]
    public string? MeaningVariants { get; set; } = default!;

    [Name("Spelling variants")]
    public string? SpellingVariants { get; set; } = default!;

    [Name("Spanish translation")]
    public string? SpanishTranslation { get; set; } = default!;

    [Name("Sub-meaning")]
    public string? SubMeaning { get; set; } = default!;

    [Name("Conjoin word")]
    public string? ConjoinWord { get; set; } = default!;

    [Name("sStrong+Instance")]
    public string? StrongInstance { get; set; } = default!;

    [Name("Alt Strongs")]
    public string? AltStrongs { get; set; } = default!;

    public List<StrongsNumberDbo> StrongsNumbers { get; set; } = default!;
}
