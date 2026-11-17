namespace UnshackledWord.Application.Features.LinkRoles;

public enum MorphemeHintKind
{
    /// <summary>The German word has no hint, or none of its hinted morphemes is in the source word.</summary>
    None,

    /// <summary>Exactly one hinted morpheme is in the source word.</summary>
    Match,

    /// <summary>Several hinted morphemes are in the source word (e.g. two pronoun suffixes of the same person).</summary>
    Ambiguous,
}

/// <summary>Result of <see cref="HebrewMorphemeHints.Find"/>. <see cref="Candidates"/> lists the morphemes of the source word that fit.</summary>
public sealed record MorphemeHint(MorphemeHintKind Kind, string? Strongs, IReadOnlyList<string> Candidates)
{
    public static readonly MorphemeHint None = new(MorphemeHintKind.None, null, []);
}

/// <summary>One row of the hint table: German STTS tags (and lemmas, null = any) → Hebrew prefix morphemes.</summary>
public sealed record MorphemeHintEntry(IReadOnlySet<string> Stts, IReadOnlySet<string>? Lemmas, IReadOnlyList<string> Strongs);

/// <summary>
/// Rule 1 of the link-role backfill (05-data-model.md): which Hebrew prefix/suffix morpheme a German word renders, by STTS tag.
/// Conjunctions → ו (H9001/H9002), articles → ה (H9009, Aramaic H9010), prepositions by lemma → ב (H9003), כ (H9004), ל (H9005, also
/// directional ה H9011 for <c>nach</c>), מ (H9006), <c>zu</c> before an infinitive → ל (H9005); personal, reflexive and possessive
/// pronouns → the pronoun suffix (H9020–H9049) of the same person, number and gender. A word without a fitting morpheme in its source
/// word gets the main morpheme (rule <see cref="LinkRoleRule.R1MainMorpheme"/>, decided by <see cref="LinkRoleRules"/>).
/// </summary>
public static class HebrewMorphemeHints
{
    /// <summary>Tags under which a preposition lemma counts. <c>PTKVZ</c> because Elb1871 tags many prepositions as particles (Mk 1:29 <c>aus der Synagoge</c>).</summary>
    public static readonly IReadOnlySet<string> PrepositionStts = new HashSet<string>(StringComparer.Ordinal) { "APPR", "APPRART", "APPO", "APZR", "PTKVZ" };

    public static readonly IReadOnlyList<MorphemeHintEntry> Table =
    [
        new(Set("KON"), null, ["H9001", "H9002"]),
        new(Set("ART"), null, ["H9009", "H9010"]),
        new(PrepositionStts, Set("in", "an", "mit", "bei"), ["H9003"]),
        new(Set("KOKOM", "KOUS", "PWAV", "APPR"), Set("wie"), ["H9004"]),
        new(PrepositionStts, Set("zu", "für"), ["H9005"]),
        new(PrepositionStts, Set("nach"), ["H9005", "H9011"]),
        new(PrepositionStts, Set("von", "aus"), ["H9006"]),
    ];

    /// <summary>The ל of an infinitive construct, rendered by <c>zu</c> before a German infinitive.</summary>
    public const string ZuInfinitiveStrongs = "H9005";

    private static readonly Dictionary<string, string> Contractions = new(StringComparer.Ordinal)
    {
        ["im"] = "in", ["ins"] = "in", ["am"] = "an", ["ans"] = "an", ["beim"] = "bei",
        ["zum"] = "zu", ["zur"] = "zu", ["vom"] = "von",
    };

    /// <summary>The prefix morphemes the table hints for a word (pronouns are matched by person instead, see <see cref="Find"/>).</summary>
    public static IReadOnlyList<string> TargetsFor(LinkRoleGermanWord word, bool zuInfinitive)
    {
        if (zuInfinitive)
        {
            return [ZuInfinitiveStrongs];
        }

        if (word.Stts is null)
        {
            return [];
        }

        var lemma = PrepositionLemma(word);
        return Table
            .Where(e => e.Stts.Contains(word.Stts) && (e.Lemmas is null || e.Lemmas.Contains(lemma)))
            .SelectMany(e => e.Strongs)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// The morpheme of <paramref name="morphemes"/> (one source word) the German word renders by rule 1. Pronouns: suffixes of the same
    /// person/number/gender; possessives prefer H9020–H9029, personal pronouns H9030–H9049, the other range only when the preferred one
    /// has none.
    /// </summary>
    public static MorphemeHint Find(LinkRoleGermanWord word, IReadOnlyList<SourceMorpheme> morphemes, bool zuInfinitive = false)
    {
        if (!zuInfinitive && (GermanPronouns.IsPersonal(word.Stts) || GermanPronouns.IsPossessive(word.Stts)))
        {
            return FindPronoun(word, morphemes);
        }

        var targets = TargetsFor(word, zuInfinitive).Select(SourceMorphemes.HebrewNumber).ToHashSet();
        var present = morphemes.Where(m => m.Number is { } n && n >= 9000 && targets.Contains(n)).Select(m => m.Strongs).ToList();
        return Result(present);
    }

    private static MorphemeHint FindPronoun(LinkRoleGermanWord word, IReadOnlyList<SourceMorpheme> morphemes)
    {
        var candidates = GermanPronouns.Candidates(word);
        if (candidates.Count == 0)
        {
            return MorphemeHint.None;
        }

        var fitting = morphemes
            .Where(m => SourceMorphemes.SuffixPgn(m.Strongs) is { } pgn && candidates.Any(c => GermanPronouns.Matches(c, pgn)))
            .ToList();
        var possessive = GermanPronouns.IsPossessive(word.Stts);
        var preferred = fitting.Where(m => SourceMorphemes.IsPossessiveSuffix(m.Strongs) == possessive).ToList();
        return Result((preferred.Count > 0 ? preferred : fitting).Select(m => m.Strongs).ToList());
    }

    private static MorphemeHint Result(IReadOnlyList<string> present) => present.Count switch
    {
        0 => MorphemeHint.None,
        1 => new MorphemeHint(MorphemeHintKind.Match, present[0], present),
        _ => new MorphemeHint(MorphemeHintKind.Ambiguous, null, present),
    };

    private static string PrepositionLemma(LinkRoleGermanWord word)
    {
        var lemma = (string.IsNullOrWhiteSpace(word.Lemma) ? word.Text : word.Lemma).Trim().ToLowerInvariant();
        return Contractions.GetValueOrDefault(lemma, lemma);
    }

    private static HashSet<string> Set(params string[] values) => new(values, StringComparer.Ordinal);
}
