namespace UnshackledWord.Application.Features.LinkRoles;

/// <summary>Which mapping table the verse comes from: Greek links carry their Strong's, Hebrew links get a morpheme of the source word.</summary>
public enum SourceLanguage
{
    Greek,
    Hebrew,
}

/// <summary>Link role of roadmap 0018 (04-roles-and-counting.md). Stored as <c>head</c> / <c>unit</c>, see <see cref="LinkRoleExtensions.ToDbValue"/>.</summary>
public enum LinkRole
{
    Head,
    Unit,
}

/// <summary>The deterministic backfill rules of 05-data-model.md. <see cref="LinkRoleExtensions.Code"/> gives the short id for reports.</summary>
public enum LinkRoleRule
{
    /// <summary>Rule 1: the German word's tag points at a prefix/suffix morpheme the source word has (<c>und</c> → H9001, <c>ihren</c> → H9024).</summary>
    R1MorphemeHint,

    /// <summary>Rule 1, fallback: no hint fits, the link goes to the braced main morpheme.</summary>
    R1MainMorpheme,

    /// <summary>Rule 2: <c>VA*</c>/<c>VM*</c> on the same morpheme as a <c>VV*</c> or participle → unit.</summary>
    R2AuxiliaryUnit,

    /// <summary>Rule 3: <c>PTKVZ</c> on the same morpheme as a finite verb whose lemma starts with the particle → unit.</summary>
    R3ParticleUnit,

    /// <summary>
    /// Rule 3b: <c>PTKVZ</c> on the same morpheme as a finite verb whose lemma does not start with the particle yet, and particle + verb
    /// lemma is a known Elb1871Morphology lemma → unit, and the verb lemma becomes particle + lemma (<c>stand … auf</c>: <c>stehen</c> →
    /// <c>aufstehen</c>). Unknown combination → undecided.
    /// </summary>
    R3bSeparableVerb,

    /// <summary>Rule 4: <c>PRF</c>, <c>PTKZU</c> or <c>zu</c> before an infinitive on the same morpheme as a verb → unit.</summary>
    R4ReflexiveOrZuUnit,

    /// <summary>Rule 5: nominative <c>PPER</c> on a verb morpheme without pronoun morpheme and with matching person → added word.</summary>
    R5SubjectPronounAdded,

    /// <summary>Rule 6: <c>PTKVZ</c> whose lemma is not its own text gets its text as lemma.</summary>
    R6ParticleLemmaReset,
}

public static class LinkRoleExtensions
{
    public static string ToDbValue(this LinkRole role) => role == LinkRole.Unit ? "unit" : "head";

    /// <summary>Short id for reports: <c>1</c>, <c>1-main</c>, <c>2</c> … <c>6</c>.</summary>
    public static string Code(this LinkRoleRule rule) => rule switch
    {
        LinkRoleRule.R1MorphemeHint => "1",
        LinkRoleRule.R1MainMorpheme => "1-main",
        LinkRoleRule.R2AuxiliaryUnit => "2",
        LinkRoleRule.R3ParticleUnit => "3",
        LinkRoleRule.R3bSeparableVerb => "3b",
        LinkRoleRule.R4ReflexiveOrZuUnit => "4",
        LinkRoleRule.R5SubjectPronounAdded => "5",
        LinkRoleRule.R6ParticleLemmaReset => "6",
        _ => rule.ToString(),
    };
}

/// <summary>
/// One verse as the rules need it. <see cref="HebRefId"/> is only carried through to the result. All words of the verse, all its links of
/// one mapping table (added-word rows included, they are ignored) and the source words those links point at.
/// </summary>
public sealed record LinkRoleVerse(
    int HebRefId,
    SourceLanguage Language,
    IReadOnlyList<LinkRoleGermanWord> Words,
    IReadOnlyList<LinkRoleLink> Links,
    IReadOnlyList<LinkRoleSourceWord> SourceWords);

/// <summary>An Elb1871Words row joined with its Elb1871Morphology row (values as stored: Person <c>1</c>–<c>3</c>, Number <c>S</c>/<c>P</c>, Gender <c>M</c>/<c>F</c>/<c>N</c>, Case <c>Nom</c>/<c>Acc</c>/<c>Dat</c>/<c>Gen</c>).</summary>
public sealed record LinkRoleGermanWord(
    int Id,
    int PositionInVerse,
    string Text,
    string? Lemma,
    string? Stts,
    string? Person = null,
    string? Number = null,
    string? Gender = null,
    string? Case = null);

/// <summary>
/// An Elb1871GreekMapping / Elb1871HebrewMapping row. <see cref="StrongsNumber"/>: Greek = the link's Strong's; Hebrew = the morpheme column
/// (NULL until backfilled; a set value is kept and rule 1 is skipped for that link).
/// </summary>
public sealed record LinkRoleLink(
    int Id,
    int GermanWordId,
    int? StepWordId,
    string? StrongsNumber,
    bool IsAddedWord = false,
    string? GermanWordPart = null);

/// <summary>A StepGreekWords row (<see cref="Morphology"/> = <c>Morphology</c>, e.g. <c>V-AAI-1S</c>) or StepHebrewWords row (<see cref="Morphology"/> = <c>Grammar</c>, e.g. <c>Hc/Vqw3ms</c>, plus <see cref="ExpandedStrongTags"/>).</summary>
public sealed record LinkRoleSourceWord(int Id, string? Morphology, string? ExpandedStrongTags = null);

/// <summary>
/// A link a rule decided. <see cref="StrongsNumber"/>: Hebrew = the morpheme the link renders (write to the link's StrongsNumber); Greek =
/// the link's Strong's unchanged. <see cref="AddedWordParentId"/> set (rule 5): the row becomes <c>IsAddedWord = true</c> with that
/// <c>ParentGermanWordId</c> (existing added rows have no StepWordId); role stays head.
/// </summary>
public sealed record LinkRoleDecision(int LinkId, int GermanWordId, LinkRoleRule Rule, LinkRole Role, string? StrongsNumber, int? AddedWordParentId = null)
{
    public bool BecomesAddedWord => AddedWordParentId is not null;
}

/// <summary>A link no rule can decide alone: conflicting rules, ambiguous morpheme or missing source data. Stays as it is; input for the model.</summary>
public sealed record LinkRoleUndecided(int LinkId, int GermanWordId, IReadOnlyList<LinkRoleRule> FittingRules, string Reason);

/// <summary>Rule 6: the <c>PTKVZ</c> token's Elb1871Morphology lemma goes from <see cref="OldLemma"/> to <see cref="NewLemma"/> (its text).</summary>
public sealed record ParticleLemmaReset(int GermanWordId, int PositionInVerse, string? OldLemma, string NewLemma)
{
    public LinkRoleRule Rule => LinkRoleRule.R6ParticleLemmaReset;
}

/// <summary>Rule 3b: the finite verb's Elb1871Morphology lemma goes from <see cref="OldLemma"/> to particle + lemma (<see cref="NewLemma"/>).</summary>
public sealed record SeparableVerbLemma(int GermanWordId, int PositionInVerse, string? OldLemma, string NewLemma, int ParticleGermanWordId)
{
    public LinkRoleRule Rule => LinkRoleRule.R3bSeparableVerb;
}

/// <summary>
/// Result for one verse. Real links that are in neither <see cref="Decisions"/> nor <see cref="Undecided"/> had no fitting rule (Greek
/// only; every Hebrew link gets at least the main-morpheme fallback) and stay head.
/// </summary>
public sealed record LinkRoleVerseResult(
    int HebRefId,
    IReadOnlyList<LinkRoleDecision> Decisions,
    IReadOnlyList<LinkRoleUndecided> Undecided,
    IReadOnlyList<ParticleLemmaReset> LemmaResets,
    IReadOnlyList<SeparableVerbLemma> VerbLemmas)
{
    /// <summary>Decisions and lemma resets per rule, for the dry-run report.</summary>
    public IReadOnlyDictionary<LinkRoleRule, int> CountsByRule()
    {
        var counts = Decisions.GroupBy(d => d.Rule).ToDictionary(g => g.Key, g => g.Count());
        if (LemmaResets.Count > 0)
        {
            counts[LinkRoleRule.R6ParticleLemmaReset] = LemmaResets.Count;
        }

        return counts;
    }
}
