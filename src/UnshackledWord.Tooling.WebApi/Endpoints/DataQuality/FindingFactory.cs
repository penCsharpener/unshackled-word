using UnshackledWord.Domain.Models.DataQuality;
using UnshackledWord.Domain.WebApi.DataQuality;

namespace UnshackledWord.Tooling.WebApi.Endpoints.DataQuality;

/// <summary>Turns a DqWordFacts row into the shared <see cref="Finding"/> shape with the check's severity and a short German reason.</summary>
internal static class FindingFactory
{
    public static IEnumerable<string?> StrongsOf(IEnumerable<FactRow> rows) => rows.Select(r => r.StrongsBase).Distinct();

    public static Finding Create(string check, FactRow r, IReadOnlyDictionary<string, LexiconEntry> lexicon, string? groupKey = null)
    {
        var (severity, detail) = Judge(check, r);
        var gloss = r.StrongsBase is not null && lexicon.TryGetValue(r.StrongsBase, out var entry) ? entry.Gloss : null;
        return new Finding
        {
            Check = check,
            Severity = severity,
            BibleBookId = r.BibleBookId,
            Chapter = r.Chapter,
            Verse = r.Verse,
            ElbWordId = r.ElbWordId,
            StepWordId = r.StepWordId,
            Lang = r.Lang,
            German = r.Plain,
            Lemma = r.Lemma,
            Pos = r.Pos,
            Original = r.SourceWord,
            Strongs = r.StrongsBase,
            Gloss = string.IsNullOrEmpty(gloss) ? null : gloss,
            Detail = detail,
            GroupKey = groupKey,
            Context = r.Context,
        };
    }

    private static (string Severity, string Detail) Judge(string check, FactRow r)
    {
        var original = $"{r.SourceWord} ({r.StrongsBase}, {r.OrigClass})";
        switch (check)
        {
            case "U1":
                return (UnmappedRules.GermanSeverity(r.Pos, r.Kind),
                    r.Kind == "none" ? $"Kein Zuordnungseintrag für „{r.Plain}“ ({r.Pos ?? "ohne Wortart"})" : $"„{r.Plain}“ ({r.Pos ?? "ohne Wortart"}) ohne Grundtextwort");
            case "U2":
            case "U3":
                var key = UnmappedRules.SourceClassKey(r.Lang ?? "G", r.OrigClass, r.StrongsBase, r.HebType, r.InNa, r.InTr);
                var cls = UnmappedRules.DescribeSource(r.Lang ?? "G", key);
                return (cls.Severity, $"{original} ohne deutsches Wort [{key}]");
            case "M1":
                return (UnmappedRules.Warning, $"„{r.Plain}“ ({r.Lemma}, {r.Pos}) ↔ {original}: weicht von der häufigsten Wiedergabe ab");
            case "M2":
                return (UnmappedRules.Warning, $"{original} ↔ „{r.Plain}“ ({r.Lemma}, {r.Pos}): seltene Wiedergabe mit unerwarteter Wortart");
            case "M3":
                return (UnmappedRules.Warning, $"Wortart {r.Pos} ↔ {r.OrigClass} ({r.SourceWord})");
            case "M4":
                return (UnmappedRules.Error, PosClassRules.IsContentGermanPos(r.Pos)
                    ? $"Inhaltswort {r.Pos} ↔ Funktionswort {r.OrigClass} ({r.SourceWord})"
                    : $"Funktionswort {r.Pos} ↔ {r.OrigClass} ({r.SourceWord})");
            case "L1":
                return (UnmappedRules.Error, !r.HasMorphologyOrPos() ? "Keine Morphologie" : r.Lemma is null ? "Lemma fehlt" : "Wortart fehlt");
            case "L2":
                return (UnmappedRules.Warning, $"Lemma „{r.Lemma}“ entspricht der Wortform ({r.Pos})");
            case "L3":
                return (UnmappedRules.Warning, $"Wortform „{r.Plain}“ mit abweichendem Lemma „{r.Lemma}“");
            case "L4":
                return (UnmappedRules.Warning, $"Lemma „{r.Lemma}“ mit abweichender Wortart {r.Pos}");
            case "L5":
                var issues = MorphologyIssues.Evaluate(new MorphologyValues(r.Pos, r.VerbForm, r.Degree, r.Tense, r.Person, r.Number, r.Mood, r.Case, r.Gender));
                return (UnmappedRules.Warning, issues.Count == 0 ? "Merkmale auffällig" : string.Join("; ", issues.Select(i => $"{i.Field}: {IssueText(i.Kind)}")));
            default:
                return (UnmappedRules.Info, r.Kind switch
                {
                    "aligned" => $"„{r.Plain}“ ↔ {original}",
                    "added" => $"„{r.Plain}“ hinzugefügt (ohne Grundtextwort)",
                    "none" => $"„{r.Plain}“ ohne Zuordnung",
                    _ => $"{original} ohne deutsches Wort",
                });
        }
    }

    private static string IssueText(string kind) => kind switch
    {
        MorphologyIssues.Missing => "fehlt",
        MorphologyIssues.NotApplicable => "gilt nicht für diese Wortart",
        _ => "unbekannter Code",
    };

    private static bool HasMorphologyOrPos(this FactRow r) => r.Pos is not null || r.Lemma is not null;
}
