namespace UnshackledWord.Domain.Models.Dbo.Step;

public sealed class StepStrongsToVersesDbo
{
    public const string DbName = "\"unshackled-word\".\"StepStrongsToVerses\"";

    public int Id { get; set; }
    public int BibleBookId { get; set; }
    public int Chapter { get; set; }
    public int Verse { get; set; }
    public int StepDisambiguatedStrongsId { get; set; }
    public bool IsRoot { get; set; }
    public string? Grammar { get; set; } = default!;
    public string Hebrew { get; set; } = default!;
    public string Gloss { get; set; } = default!;
    public string? Name { get; set; }
    public int? FirstOccuranceBibleBookId { get; set; }
    public int? FirstOccuranceChapter { get; set; }
    public int? FirstOccuranceVerse { get; set; }
    public int? LastOccuranceBibleBookId { get; set; }
    public int? LastOccuranceChapter { get; set; }
    public int? LastOccuranceVerse { get; set; }
    // max 20 chars long
    public string StrongsNumber { get; set; } = default!;

    // has unique index on (BibleBookId, Chapter, Verse, StrongsNumber)
}

public sealed class StepDisambiguatedStrongsDbo
{
    public const string DbName = "\"unshackled-word\".\"StepDisambiguatedStrongs\"";

    public int Id { get; set; }
    public int StepHebrewWordId { get; set; }
    public bool IsRoot { get; set; }
    public string? Grammar { get; set; } = default!;
    public string? Hebrew { get; set; } = default!;
    public string? Gloss { get; set; } = default!;
    public string? Name { get; set; }
    public int? FirstOccuranceBibleBookId { get; set; }
    public int? FirstOccuranceChapter { get; set; }
    public int? FirstOccuranceVerse { get; set; }
    public int? LastOccuranceBibleBookId { get; set; }
    public int? LastOccuranceChapter { get; set; }
    public int? LastOccuranceVerse { get; set; }
    public string StrongsNumber { get; set; } = default!;
}
