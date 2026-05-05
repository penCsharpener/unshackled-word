namespace UnshackledWord.Tooling.SeedDb.Services.ElbMorphology;

public class PartOfSpeech : SmartEnum<PartOfSpeech>
{
    public static readonly PartOfSpeech Adjective = new(1, ["ADJ"], "Adjective", "Adjektiv");
    public static readonly PartOfSpeech Adposition = new(2, ["ADP"], "Adposition", "Adposition");
    public static readonly PartOfSpeech Adverb = new(3, ["ADV"], "Adverb", "Adverb");
    public static readonly PartOfSpeech Auxiliary = new(4, ["AUX"], "Auxiliary", "Hilfsverb");
    public static readonly PartOfSpeech CoordinatingConjunction = new(5, ["CCONJ"], "Coordinating Conjunction", "Nebenordnende Konjunktion");
    public static readonly PartOfSpeech Determiner = new(6, ["DET"], "Determiner", "Determinativ");
    public static readonly PartOfSpeech Interjection = new(7, ["INTJ"], "Interjection", "Interjektion");
    public static readonly PartOfSpeech Noun = new(8, ["NOUN"], "Noun", "Substantiv");
    public static readonly PartOfSpeech Number = new(9, ["NUM"], "Number", "Numerale");
    public static readonly PartOfSpeech Particle = new(10, ["PART"], "Particle", "Partikel");
    public static readonly PartOfSpeech Pronoun = new(11, ["PRON"], "Pronoun", "Pronomen");
    public static readonly PartOfSpeech ProperNoun = new(12, ["PROPN"], "Proper Noun", "Eigenname");
    public static readonly PartOfSpeech Punctuation = new(13, ["PUNCT"], "Punctuation", "Interpunktion");
    public static readonly PartOfSpeech SubordinatingConjunction = new(14, ["SCONJ"], "Subordinating Conjunction", "Unterordnende Konjunktion");
    public static readonly PartOfSpeech Space = new(15, ["SPACE"], "Space", "Leerzeichen");
    public static readonly PartOfSpeech Verb = new(16, ["VERB"], "Verb", "Verb");
    public static readonly PartOfSpeech Other = new(17, ["X"], "Other", "Sonstiges");

    public PartOfSpeech(int id, string[] abbreviation, string english, string german) : base(id, abbreviation, english, german)
    {
    }
}
