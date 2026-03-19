using UnshackledWord.Domain.Extensions;

namespace UnshackledWord.Domain.Models.Dbo.Step;

public sealed class StepHebrewWordDbo : IBibleWordOrderColumns
{
    public const string DbName = "\"unshackled-word\".\"StepHebrewWords\"";

    public int Id { get; set; }
    public int BibleBookId { get; set; }
    public int Chapter { get; set; }
    public int Verse { get; set; }
    public int RefId { get; set; }
    public int PositionInVerse { get; set; }
    public int? AltChapter { get; set; }
    public int? AltVerse { get; set; }
    public string Type { get; set; } = default!;
    public string HebrewNormalised { get; set; } = default!;
    public string Hebrew { get; set; } = default!;
    public string HebrewNoDiacritics { get; set; } = default!;
    public string Transliteration { get; set; } = default!;
    public string Gloss { get; set; } = default!;
    public string DisambiguatedStrongs { get; set; } = default!;
    public string Grammar { get; set; } = default!;
    public string? MeaningVariants { get; set; }
    public string? SpellingVariants { get; set; }
    public string? RootDisambiguatedStrongsInstance { get; set; }
    public string? AlternativeStrongs { get; set; }
    public string? ConjoinWord { get; set; }
    public string? ExpandedStrongTags { get; set; }
    public ICollection<StepHebrewWordsNormalizedDbo> NormalizedWords { get; set; } = [];

    public string GetManuscriptMeaning()
    {
        if (Type.IsNullOrWhiteSpace())
        {
            return Type;
        }

        // Matches the leading character (case-insensitive)
        return char.ToUpper(Type[0]) switch
        {
            'L' => "Leningrad Manuscript",
            'R' => "Restored Text (Leningrad Parallels)",
            'X' => "Greek Sources (LXX Emendation)",
            'Q' => "Qere (Scribal spoken correction)",
            'K' => "Ketiv (Uncorrected written text)",
            'A' => "Aleppo Manuscript",
            'B' => "BHS (Biblia Hebraica Stuttgartensia)",
            'C' => "Cairensis Manuscript",
            'D' => "Dead Sea or Judean Desert Manuscript",
            'E' => "Ancient Source Emendation",
            'F' => "Formatting (Re-pointing/Division)",
            'H' => "Ben Chaim Edition",
            'P' => "Alternate Punctuation",
            'S' => "Scribal Traditions (Tiqqune Sopherim)",
            'V' => "Manuscript Variant",
            _   => "Unknown Source"
        };
    }
}
