using System.Text.RegularExpressions;

namespace UnshackledWord.Domain.Models.Grammar;

/// <summary>One cell of the original-language breakdown of a search: how often a German (PoS) is aligned to an original key / class.</summary>
public sealed record OriginalCell(string Key, string Kind, string? Word, string? Gloss, string? OrigPos, string? GermanPos, long Count);

public sealed record OriginalGroup(
    string Key,
    string Kind,
    string? Lang,
    string? Word,
    string? Gloss,
    string? Pos,
    long Count,
    double Share,
    bool Minority,
    bool PosConflict,
    bool Unmapped,
    bool Added);

public sealed record OriginalSummary(IReadOnlyList<OriginalGroup> Groups, bool Ambiguous);

/// <summary>
/// Pure rules of the original-language guard: which German part of speech may align with which original class,
/// flags per original group and the server-computed ambiguity hint. The SQL only delivers counts.
/// </summary>
public static partial class OriginalLanguageRules
{
    public const string KindAligned = "aligned";
    public const string KindMulti = "multi";
    public const string KindAdded = "added";
    public const string KindNone = "none";

    public const double MinorityShare = 0.05;
    public const double AmbiguityShare = 0.10;
    public const int MaxOriginalFilters = 20;

    private static readonly HashSet<string> HebrewFunctionWordPos = ["DET", "ADP", "CCONJ", "SCONJ", "PRON", "AUX", "PART"];

    private static readonly HashSet<string> GlossStopWords = ["be", "to", "a", "an", "the", "of", "or", "and", "in", "that", "which"];

    [GeneratedRegex(@"^(?:[GH]\d{4,5}(?:\+[GH]\d{4,5})*|added|none)$")]
    private static partial Regex OriginalKeyRegex();

    /// <summary>"G4151", "H7307", a '+'-joined multi key, "added" or "none".</summary>
    public static bool IsValidOriginalKey(string? key) => key is not null && OriginalKeyRegex().IsMatch(key);

    /// <summary>The empty string is accepted for ExpectedOriginal: it stands for added or none.</summary>
    public static bool IsValidExpectedOriginal(string? key) => key == string.Empty || IsValidOriginalKey(key);

    public static bool KeyMatchesExpected(string expected, string actualKey) =>
        expected == actualKey || (expected.Length == 0 && actualKey is KindAdded or KindNone);

    /// <summary>
    /// Original word classes (first letter of the STEP morphology) a German UPOS may align with, or null when no check applies.
    /// Greek classes: N V A T R P D F I X, CONJ PREP PRT COND ADV INJ; Hebrew: N V A R C T D S P.
    /// </summary>
    public static IReadOnlySet<string>? ExpectedOriginalPos(string? germanPos) => germanPos switch
    {
        "NOUN" or "PROPN" => NounClasses,
        "VERB" or "AUX" => VerbClasses,
        "DET" => ["T", "D", "P", "R"],
        "PRON" => ["P", "R", "D", "F", "I", "X"],
        "ADJ" or "NUM" => NounClasses,
        "ADP" => ["PREP", "R", "S"],
        "CCONJ" or "SCONJ" => ["CONJ", "COND", "C", "PREP"],
        _ => null,
    };

    private static readonly HashSet<string> NounClasses = ["N", "A"];
    private static readonly HashSet<string> VerbClasses = ["V"];

    public static bool IsHebrewFunctionWordPos(string? germanPos) => germanPos is not null && HebrewFunctionWordPos.Contains(germanPos);

    /// <summary>"G" or "H" for aligned/multi keys, otherwise null.</summary>
    public static string? LangOf(string key, string kind) => kind is KindAligned or KindMulti && key.Length > 0 ? key[..1] : null;

    /// <summary>Per-row flag: aligned, German PoS has an expectation, the original class is outside it, and it is not an OT function word.</summary>
    public static bool RowPosConflict(string? germanPos, string? origPos, string key, string kind)
    {
        if (kind is not (KindAligned or KindMulti) || string.IsNullOrEmpty(origPos)) return false;
        var expected = ExpectedOriginalPos(germanPos);
        if (expected is null) return false;
        if (LangOf(key, kind) == "H" && IsHebrewFunctionWordPos(germanPos)) return false;
        return !expected.Contains(origPos);
    }

    /// <summary>True when the two glosses share no meaningful word, i.e. they are not synonyms ("Lord" / "lord" share one, "wise" / "way" do not). Missing gloss: no evidence, so false.</summary>
    public static bool GlossesDiffer(string? a, string? b)
    {
        var ta = GlossTokens(a);
        var tb = GlossTokens(b);
        if (ta.Count == 0 || tb.Count == 0) return false;
        return !ta.Overlaps(tb);
    }

    private static HashSet<string> GlossTokens(string? gloss) =>
        (gloss ?? string.Empty)
            .ToLowerInvariant()
            .Split([' ', ',', ';', ':', '/', '(', ')', '[', ']', '-', '.', '\'', '"'], StringSplitOptions.RemoveEmptyEntries)
            .Where(t => !GlossStopWords.Contains(t))
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>Builds the groups (one per original key) with share and flags, plus the ambiguity hint.</summary>
    public static OriginalSummary Summarize(IReadOnlyList<OriginalCell> cells)
    {
        var byKey = cells.GroupBy(c => c.Key).Select(g =>
        {
            var first = g.First();
            var count = g.Sum(c => c.Count);
            var dominant = g.OrderByDescending(c => c.Count).First();
            var lang = LangOf(first.Key, first.Kind);
            var functionCount = g.Where(c => IsHebrewFunctionWordPos(c.GermanPos)).Sum(c => c.Count);
            var suppressed = lang == "H" && functionCount * 2 >= count;
            var conflictCount = g.Where(c => RowPosConflict(c.GermanPos, c.OrigPos, c.Key, c.Kind)).Sum(c => c.Count);
            return new
            {
                Key = first.Key,
                first.Kind,
                Lang = lang,
                Word = g.Select(c => c.Word).FirstOrDefault(w => !string.IsNullOrEmpty(w)),
                Gloss = g.Select(c => c.Gloss).FirstOrDefault(w => !string.IsNullOrEmpty(w)),
                Pos = dominant.OrigPos,
                Count = count,
                Suppressed = suppressed,
                PosConflict = !suppressed && conflictCount * 2 > count,
            };
        }).ToList();

        var alignedByLang = byKey.Where(k => k.Lang is not null).GroupBy(k => k.Lang!).ToDictionary(g => g.Key, g => g.ToList());
        var total = byKey.Sum(k => k.Count);

        var groups = new List<OriginalGroup>();
        var ambiguous = false;

        foreach (var k in byKey.OrderByDescending(x => x.Count).ThenBy(x => x.Key, StringComparer.Ordinal))
        {
            double share;
            var minority = false;
            if (k.Lang is { } lang)
            {
                var list = alignedByLang[lang];
                var langTotal = list.Sum(x => x.Count);
                share = langTotal == 0 ? 0 : (double)k.Count / langTotal;
                var isTop = list.OrderByDescending(x => x.Count).ThenBy(x => x.Key, StringComparer.Ordinal).First().Key == k.Key;
                minority = !k.Suppressed && !isTop && share < MinorityShare;
            }
            else
            {
                share = total == 0 ? 0 : (double)k.Count / total;
            }

            groups.Add(new OriginalGroup(k.Key, k.Kind, k.Lang, k.Word, k.Gloss, k.Pos, k.Count, share, minority, k.PosConflict,
                k.Kind == KindNone, k.Kind == KindAdded));
        }

        if (groups.Any(g => g.PosConflict)) ambiguous = true;

        foreach (var lang in new[] { "G", "H" })
        {
            var strong = groups
                .Where(g => g.Lang == lang && g.Share >= AmbiguityShare && !byKey.First(k => k.Key == g.Key).Suppressed)
                .ToList();
            for (var i = 0; i < strong.Count && !ambiguous; i++)
            {
                for (var j = i + 1; j < strong.Count; j++)
                {
                    if (GlossesDiffer(strong[i].Gloss, strong[j].Gloss))
                    {
                        ambiguous = true;
                        break;
                    }
                }
            }
        }

        return new OriginalSummary(groups, ambiguous);
    }
}
