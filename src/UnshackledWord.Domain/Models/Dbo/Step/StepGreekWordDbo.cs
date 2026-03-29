namespace UnshackledWord.Domain.Models.Dbo.Step;

public sealed class StepGreekWordDbo : IBibleWordOrderColumns
{
    public const string DbName = "\"unshackled-word\".\"StepGreekWords\"";

    public int Id { get; set; }
    public int LxxRefId { get; set; }
    public int PositionInVerse { get; set; }
    public int? AltChapter { get; set; }
    public int? AltVerse { get; set; }
    public string Type { get; set; } = default!;
    public bool IsInNestleAland { get; set; }
    public bool IsInTextusReceptus { get; set; }
    public bool IsInOther { get; set; }
    public string Greek { get; set; } = default!;
    public string GreekNoDiacritics { get; set; } = default!;
    public string Transliteration { get; set; } = default!;
    public string English { get; set; } = default!;
    public string? German { get; set; }
    public string? Spanish { get; set; }
    public string DisambiguatedStrongs { get; set; } = default!;
    public string Morphology { get; set; } = default!;
    public string Lemma { get; set; } = default!;
    public string LemmaNoDiacritics { get; set; } = default!;
    public string Gloss { get; set; } = default!;
    public string Editions { get; set; } = default!;
    public string? MeaningVariants { get; set; }
    public string? SpellingVariants { get; set; }
    public string? SubMeaning { get; set; }
    public string? ConjoinWord { get; set; }
    public string? StrongInstance { get; set; }
    public string? AltStrongs { get; set; }
    public List<StrongsNumberDbo> StrongsNumbers { get; set; } = default!;
}
