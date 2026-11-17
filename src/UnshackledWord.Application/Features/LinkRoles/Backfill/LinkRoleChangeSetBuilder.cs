using System.Text.RegularExpressions;
using UnshackledWord.Domain.WebApi.Maintainer;

namespace UnshackledWord.Application.Features.LinkRoles.Backfill;

/// <summary>The change set of one backfill run: mapping rows and lemma rows to write, and the report shown before they are written.</summary>
public sealed record LinkRoleChangeSet(IReadOnlyList<MappingRowChange> Mappings, IReadOnlyList<LemmaRowChange> Lemmas, LinkRoleReport Report);

/// <summary>
/// Turns the rule decisions of <see cref="LinkRoleRules"/> into row changes, book by book (roadmap 0018.4). Pure: the caller loads the
/// books. Only rows whose values change are written. A decision becomes:
/// <list type="bullet">
/// <item>rules 1–4: <c>Role</c> and (Hebrew) <c>StrongsNumber</c> of the row;</item>
/// <item>rule 5: <c>IsAddedWord = true</c>, <c>ParentGermanWordId</c> = the verb, <c>StepWordId</c> and <c>StrongsNumber</c> NULL (like rows
/// the editor saves), role head. A pronoun with other rows in the same table stays undecided, the unique (ElbWordId, StepWordId) key would
/// not allow two added rows;</item>
/// <item>rule 3b: also the verb's lemma; rule 6: the particle's lemma (both written as Elb1871MorphologyEdits of the change set).</item>
/// </list>
/// </summary>
public sealed partial class LinkRoleChangeSetBuilder
{
    public const string Greek = "greek";
    public const string Hebrew = "hebrew";
    public const string German = "german";

    private const int SampleBuffer = 200;

    private readonly IReadOnlySet<string> _knownLemmas;
    private readonly List<MappingRowChange> _mappings = [];
    private readonly Dictionary<int, LemmaRowChange> _lemmas = [];
    private readonly LinkRoleReport _report = new();
    private readonly Dictionary<string, List<(uint Order, LinkRoleSample Sample)>> _samples = [];

    public LinkRoleChangeSetBuilder(IReadOnlySet<string> knownLemmas)
    {
        _knownLemmas = knownLemmas;
    }

    public void AddBook(LinkRoleBookData book)
    {
        var greekRows = RowsByVerse(book.GreekRows, book.Words);
        var hebrewRows = RowsByVerse(book.HebrewRows, book.Words);
        var greekSources = book.GreekSources.ToDictionary(s => s.Id);
        var hebrewSources = book.HebrewSources.ToDictionary(s => s.Id);

        foreach (var verseWords in book.Words.GroupBy(w => w.HebRefId))
        {
            var words = verseWords.OrderBy(w => w.PositionInVerse).ThenBy(w => w.Id).ToList();
            var reference = $"{book.BookName} {words[0].Chapter}:{words[0].Verse}";
            var counted = false;

            foreach (var (language, rows, sources) in new[]
                     {
                         (SourceLanguage.Greek, greekRows, greekSources),
                         (SourceLanguage.Hebrew, hebrewRows, hebrewSources),
                     })
            {
                if (!rows.TryGetValue(verseWords.Key, out var verseRows))
                {
                    continue;
                }

                counted = true;
                AddVerse(verseWords.Key, reference, language, words, verseRows, sources);
            }

            var resets = LinkRoleRules.ParticleLemmaResets(words.Select(w => w.ToGermanWord()));
            if (resets.Count > 0)
            {
                counted = true;
                var byId = words.ToDictionary(w => w.Id);
                var changes = resets
                    .Select(r => AddLemma(new LemmaRowChange(r.GermanWordId, verseWords.Key, r.PositionInVerse, r.Rule.Code(), r.OldLemma, r.NewLemma)))
                    .OfType<LemmaRowChange>()
                    .Select(c => new LinkRoleSampleChange { Word = byId[c.ElbWordId].Text, Before = $"lemma {c.OldLemma}", After = $"lemma {c.NewLemma}" })
                    .ToList();
                Count(_report.Decided, German, LinkRoleRule.R6ParticleLemmaReset.Code(), resets.Count);
                AddSample(LinkRoleRule.R6ParticleLemmaReset.Code(), verseWords.Key, reference, German, words, changes);
            }

            if (counted)
            {
                _report.Verses++;
            }
        }
    }

    public LinkRoleChangeSet Build()
    {
        _report.MappingRows = _mappings.Count;
        _report.LemmaRows = _lemmas.Count;
        _report.Samples = _samples.OrderBy(s => s.Key, StringComparer.Ordinal).ToDictionary(
            s => s.Key,
            s => s.Value.OrderBy(x => x.Order).Take(LinkRoleReport.SamplesPerRule).Select(x => x.Sample).OrderBy(x => x.HebRefId).ToList());

        return new LinkRoleChangeSet(
            _mappings.OrderBy(m => m.Lang).ThenBy(m => m.MappingId).ToList(),
            _lemmas.Values.OrderBy(l => l.HebRefId).ThenBy(l => l.PositionInVerse).ToList(),
            _report);
    }

    /// <summary>The values a decision gives a row.</summary>
    public static MappingRowValues After(LinkRoleDecision decision, MappingRowValues before, SourceLanguage language) => decision.BecomesAddedWord
        ? new MappingRowValues(LinkRole.Head.ToDbValue(), null, true, decision.AddedWordParentId, null)
        : before with
        {
            Role = decision.Role.ToDbValue(),
            StrongsNumber = language == SourceLanguage.Hebrew ? decision.StrongsNumber : before.StrongsNumber,
        };

    /// <summary>Groups an undecided reason: <c>source word 123 has several main morphemes: H1, H2</c> → <c>source word has several main morphemes</c>.</summary>
    public static string ReasonGroup(string reason)
    {
        var text = SourceWordId().Replace(reason, "source word");
        var colon = text.IndexOf(':', text.StartsWith("rule 1:", StringComparison.Ordinal) ? "rule 1:".Length : 0);
        return colon < 0 ? text : text[..colon];
    }

    private void AddVerse(
        int hebRefId,
        string reference,
        SourceLanguage language,
        List<LinkRoleWordRow> words,
        List<LinkRoleMappingRow> rows,
        Dictionary<int, LinkRoleSourceRow> sources)
    {
        var lang = language == SourceLanguage.Hebrew ? Hebrew : Greek;
        var verse = new LinkRoleVerse(
            hebRefId,
            language,
            words.Select(w => w.ToGermanWord()).ToList(),
            rows.Select(r => new LinkRoleLink(r.Id, r.ElbWordId, r.StepWordId, r.StrongsNumber, r.IsAddedWord == true, r.GermanWordPart)).ToList(),
            rows.Where(r => r.StepWordId is not null).Select(r => r.StepWordId!.Value).Distinct()
                .Where(sources.ContainsKey)
                .Select(id => new LinkRoleSourceWord(id, sources[id].Morphology, sources[id].MorphemeTags))
                .ToList());
        var result = LinkRoleRules.Decide(verse, _knownLemmas);
        _report.Links += rows.Count(r => r.IsAddedWord != true && r.StepWordId is not null);

        var rowsById = rows.ToDictionary(r => r.Id);
        var wordsById = words.ToDictionary(w => w.Id);
        var samples = new Dictionary<string, List<LinkRoleSampleChange>>();
        foreach (var decision in result.Decisions)
        {
            var code = decision.Rule.Code();
            var row = rowsById[decision.LinkId];
            if (decision.BecomesAddedWord && rows.Any(r => r.ElbWordId == row.ElbWordId && r.Id != row.Id))
            {
                AddUndecided(lang, "rule 5: subject pronoun with several rows");
                continue;
            }

            Count(_report.Decided, lang, code);
            var before = row.Values;
            var after = After(decision, before, language);
            if (after == before)
            {
                continue;
            }

            _mappings.Add(new MappingRowChange(MappingLang.Of(language), row.Id, hebRefId, row.ElbWordId, code, before, after));
            Count(_report.Changed, lang, code);
            Sample(samples, code).Add(new LinkRoleSampleChange
            {
                Word = wordsById[row.ElbWordId].Text,
                Before = Describe(before, wordsById),
                After = Describe(after, wordsById),
            });
        }

        foreach (var undecided in result.Undecided)
        {
            AddUndecided(lang, ReasonGroup(undecided.Reason));
        }

        foreach (var verb in result.VerbLemmas)
        {
            Count(_report.Decided, German, verb.Rule.Code());
            if (AddLemma(new LemmaRowChange(verb.GermanWordId, hebRefId, verb.PositionInVerse, verb.Rule.Code(), verb.OldLemma, verb.NewLemma)) is { } change)
            {
                Sample(samples, change.Rule).Add(new LinkRoleSampleChange { Word = wordsById[verb.GermanWordId].Text, Before = $"lemma {change.OldLemma}", After = $"lemma {change.NewLemma}" });
            }
        }

        foreach (var (code, changes) in samples)
        {
            AddSample(code, hebRefId, reference, lang, words, changes);
        }
    }

    /// <summary>Adds a lemma change unless it changes nothing or the word already has one (first wins); returns the added change.</summary>
    private LemmaRowChange? AddLemma(LemmaRowChange change)
    {
        if (string.Equals(change.OldLemma, change.NewLemma, StringComparison.Ordinal) || !_lemmas.TryAdd(change.ElbWordId, change))
        {
            return null;
        }

        Count(_report.Changed, German, change.Rule);
        return change;
    }

    private void AddUndecided(string lang, string reason)
    {
        _report.Undecided++;
        Count(_report.UndecidedByReason, lang, reason);
    }

    private void AddSample(string code, int hebRefId, string reference, string lang, List<LinkRoleWordRow> words, List<LinkRoleSampleChange> changes)
    {
        if (changes.Count == 0)
        {
            return;
        }

        if (!_samples.TryGetValue(code, out var list))
        {
            _samples[code] = list = [];
        }

        // A multiplicative hash of the verse spreads the samples over the Bible and keeps them the same from run to run.
        list.Add((unchecked((uint)hebRefId * 2654435761u), new LinkRoleSample
        {
            HebRefId = hebRefId,
            Reference = reference,
            Language = lang,
            Text = string.Join(" ", words.Select(w => w.Text)),
            Changes = changes,
        }));
        if (list.Count > SampleBuffer)
        {
            list.Sort((a, b) => a.Order.CompareTo(b.Order));
            list.RemoveRange(LinkRoleReport.SamplesPerRule, list.Count - LinkRoleReport.SamplesPerRule);
        }
    }

    private static List<LinkRoleSampleChange> Sample(Dictionary<string, List<LinkRoleSampleChange>> samples, string code)
    {
        if (!samples.TryGetValue(code, out var list))
        {
            samples[code] = list = [];
        }

        return list;
    }

    private static string Describe(MappingRowValues values, Dictionary<int, LinkRoleWordRow> words) => values.IsAddedWord == true
        ? $"added → {(values.ParentGermanWordId is { } p && words.TryGetValue(p, out var parent) ? parent.Text : values.ParentGermanWordId?.ToString())}"
        : $"{values.Role} {values.StrongsNumber ?? "(main)"}";

    private static void Count(Dictionary<string, Dictionary<string, int>> counts, string lang, string key, int by = 1)
    {
        if (!counts.TryGetValue(lang, out var inner))
        {
            counts[lang] = inner = [];
        }

        inner[key] = inner.GetValueOrDefault(key) + by;
    }

    /// <summary>Mapping rows per verse (HebRefId of their German word); rows of words outside the book are left out.</summary>
    private static Dictionary<int, List<LinkRoleMappingRow>> RowsByVerse(IReadOnlyList<LinkRoleMappingRow> rows, IReadOnlyList<LinkRoleWordRow> words)
    {
        var verseOf = words.ToDictionary(w => w.Id, w => w.HebRefId);
        return rows.Where(r => verseOf.ContainsKey(r.ElbWordId)).GroupBy(r => verseOf[r.ElbWordId]).ToDictionary(g => g.Key, g => g.ToList());
    }

    [GeneratedRegex(@"source word \d+")]
    private static partial Regex SourceWordId();
}
