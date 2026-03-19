namespace UnshackledWord.Domain.Models.Dbo;

public sealed class SrGntWordsDbo
{
    public const string DboName = "\"unshackled-word\".\"SrGntWords\"";

    public int Id { get; set; }
    public int BibleBookId { get; set; }
    public int Chapter { get; set; }
    public int Verse { get; set; }
    public int RefId { get; set; }
    public string WordInContext { get; set; } = default!;
    public string Koine { get; set; } = default!;
    public string Lemma { get; set; } = default!;
    public int PositionInVerse { get; set; }
    public string Strongs { get; set; } = default!;
    public string PartOfSpeech { get; set; } = default!;
    public string GrammaticalKey { get; set; } = default!;
    public int? Mood { get; set; }
    public int? Tense { get; set; }
    public int? Voice { get; set; }
    public int? Person { get; set; }
    public int? Case { get; set; }
    public int? Gender { get; set; }
    public int? Number { get; set; }
}