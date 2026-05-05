namespace UnshackledWord.Tooling.SeedDb.Services.ElbMorphology;

/// <summary>
/// Stuttgart-Tübingen-Tagset (STTS) for German part-of-speech tagging.
/// </summary>
public class SttsTag : SmartEnum<SttsTag>
{
    public static readonly SttsTag AdjectiveAttributive = new(1, "ADJA", "Attributive Adjective", "Attributives Adjektiv");
    public static readonly SttsTag AdjectiveAdverbial = new(2, "ADJD", "Adverbial Adjective", "Adverbiales oder prädikatives Adjektiv");
    public static readonly SttsTag Adverb = new(3, "ADV", "Adverb", "Adverb");
    public static readonly SttsTag Postposition = new(4, "APPO", "Postposition", "Postposition");
    public static readonly SttsTag Preposition = new(5, "APPR", "Preposition", "Präposition");
    public static readonly SttsTag PrepositionWithArticle = new(6, "APPRART", "Preposition with Article", "Präposition mit Artikel");
    public static readonly SttsTag RightCircumposition = new(7, "APZR", "Right Circumposition", "Rechter Zirkumpositionsteil");
    public static readonly SttsTag Article = new(8, "ART", "Article", "Artikel");
    public static readonly SttsTag CardinalNumber = new(9, "CARD", "Cardinal Number", "Kardinalzahl");
    public static readonly SttsTag ForeignMaterial = new(10, "FM", "Foreign Material", "Fremdsprachliches Material");
    public static readonly SttsTag Interjection = new(11, "ITJ", "Interjection", "Interjektion");
    public static readonly SttsTag ComparisonConjunction = new(12, "KOKOM", "Comparison Conjunction", "Vergleichskonjunktion");
    public static readonly SttsTag CoordinatingConjunction = new(13, "KON", "Coordinating Conjunction", "Nebenordnende Konjunktion");
    public static readonly SttsTag SubjunctionWithZu = new(14, "KOUI", "Subjunction with 'zu'", "Unterordnende Konjunktion mit 'zu'");
    public static readonly SttsTag Subjunction = new(15, "KOUS", "Subjunction", "Unterordnende Konjunktion");
    public static readonly SttsTag ProperNoun = new(16, "NE", "Proper Noun", "Eigennamen");
    public static readonly SttsTag CommonNoun = new(17, "NN", "Common Noun", "Gattungsname");
    public static readonly SttsTag AttributiveDemonstrativePronoun = new(18, "PDAT", "Attributive Demonstrative Pronoun", "Attribuierendes Demonstrativpronomen");
    public static readonly SttsTag SubstitutingDemonstrativePronoun = new(19, "PDS", "Substituting Demonstrative Pronoun", "Substituierendes Demonstrativpronomen");
    public static readonly SttsTag AttributiveIndefinitePronoun = new(20, "PIAT", "Attributive Indefinite Pronoun", "Attribuierendes Indefinitpronomen");
    public static readonly SttsTag SubstitutingIndefinitePronoun = new(21, "PIS", "Substituting Indefinite Pronoun", "Substituierendes Indefinitpronomen");
    public static readonly SttsTag PersonalPronoun = new(22, "PPER", "Personal Pronoun", "Personales Pronomen");
    public static readonly SttsTag AttributivePossessivePronoun = new(23, "PPOSAT", "Attributive Possessive Pronoun", "Attribuierendes Possessivpronomen");
    public static readonly SttsTag ReflexivePronoun = new(24, "PRF", "Reflexive Pronoun", "Reflexivpronomen");
    public static readonly SttsTag PronominalAdverb = new(25, "PROAV", "Pronominal Adverb", "Pronominaladverb");
    public static readonly SttsTag ParticleAdverbDegree = new(26, "PTKA", "Particle with Adjective/Adverb", "Partikel bei Adjektiv oder Adverb");
    public static readonly SttsTag AnswerParticle = new(27, "PTKANT", "Answer Particle", "Antwortpartikel");
    public static readonly SttsTag NegativeParticle = new(28, "PTKNEG", "Negative Particle", "Negationspartikel");
    public static readonly SttsTag SeparableVerbPrefix = new(29, "PTKVZ", "Separable Verb Prefix", "Abgetrennter Verbzusatz");
    public static readonly SttsTag AttributiveInterrogativePronoun = new(30, "PWAT", "Attributive Interrogative Pronoun", "Attribuierendes Interrogativpronomen");
    public static readonly SttsTag InterrogativeAdverb = new(31, "PWAV", "Interrogative Adverb", "Adverbiales Interrogativpronomen");
    public static readonly SttsTag SubstitutingInterrogativePronoun = new(32, "PWS", "Substituting Interrogative Pronoun", "Substituierendes Interrogativpronomen");
    public static readonly SttsTag Space = new(33, "_SP", "Space", "Leerzeichen");
    public static readonly SttsTag TruncatedWord = new(34, "TRUNC", "Truncated Word", "Wortabschnitt");
    public static readonly SttsTag AuxiliaryFinite = new(35, "VAFIN", "Finite Auxiliary Verb", "Finites Hilfsverb");
    public static readonly SttsTag AuxiliaryInfinitive = new(36, "VAINF", "Infinitive Auxiliary Verb", "Infinitiv Hilfsverb");
    public static readonly SttsTag AuxiliaryParticiple = new(37, "VAPP", "Participle Auxiliary Verb", "Partizip Hilfsverb");
    public static readonly SttsTag ModalFinite = new(38, "VMFIN", "Finite Modal Verb", "Finites Modalverb");
    public static readonly SttsTag ModalParticiple = new(39, "VMPP", "Participle Modal Verb", "Partizip Modalverb");
    public static readonly SttsTag MainFinite = new(40, "VVFIN", "Finite Main Verb", "Finites Vollverb");
    public static readonly SttsTag MainImperative = new(41, "VVIMP", "Imperative Main Verb", "Imperativ Vollverb");
    public static readonly SttsTag MainInfinitive = new(42, "VVINF", "Infinitive Main Verb", "Infinitiv Vollverb");
    public static readonly SttsTag MainInfinitiveWithZu = new(43, "VVIZU", "Infinitive Main Verb with 'zu'", "Infinitiv Vollverb mit 'zu'");
    public static readonly SttsTag MainParticiple = new(44, "VVPP", "Participle Main Verb", "Partizip Vollverb");
    public static readonly SttsTag NonWord = new(45, "XY", "Non-Word/Symbol", "Nichtwort / Sonderzeichen");

    private SttsTag(int id, string abbreviation, string english, string german)
        : base(id, [abbreviation], english, german) { }
}
