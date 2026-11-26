using System.Diagnostics;
using Microsoft.Extensions.Logging;
using UnshackledWord.Domain.Models.BibleStructure;

namespace UnshackledWord.Application.Features.Lxx;

/// <summary>Coverage of one book (and text form) after pass 1.</summary>
public sealed record LxxPrefillBookCoverage(int BibleBookId, string TextForm, int Words, int Proposed, int Covered);

/// <summary>Result of <see cref="LxxNtFormTagger.RunAsync"/>.</summary>
public sealed record LxxPrefillResult
{
    public int TotalWords { get; init; }

    /// <summary>Words that now carry a pass-1 proposal.</summary>
    public int Proposed { get; init; }

    /// <summary>Words skipped because the form has more than one NT analysis.</summary>
    public int Ambiguous { get; init; }

    /// <summary>Words skipped because the normalized form does not occur in the NT.</summary>
    public int Unknown { get; init; }

    /// <summary>Words skipped as unaccented capitals (Swete's book openings).</summary>
    public int SkippedCapitals { get; init; }

    /// <summary>Words skipped because the unique NT analysis has a Strong's or morphology that does not validate.</summary>
    public int Invalid { get; init; }

    /// <summary>Words with no letters after normalization.</summary>
    public int Empty { get; init; }

    /// <summary>Words with an existing row that is not an <c>nt-form</c> proposal (confirmed, ai, human): never touched.</summary>
    public int KeptExisting { get; init; }

    /// <summary>Words covered by a tag of any source after the run (pass-1 proposals plus the kept existing rows).</summary>
    public int Covered { get; init; }

    public int Inserted { get; init; }
    public int Updated { get; init; }
    public int Deleted { get; init; }
    public int Changed => Inserted + Updated + Deleted;
    public TimeSpan Duration { get; init; }
    public IReadOnlyList<LxxPrefillBookCoverage> Books { get; init; } = [];
}

/// <summary>
/// Pass 1 of the LXX tagging (roadmap 0028.6, <c>docs/roadmaps/0028-lxx-rahlfs/05-tagging.md</c>): an LXX word whose normalized form
/// (<see cref="LxxFormNormalizer"/>) occurs in the NT with exactly one distinct (Lemma, Strong's, Morphology) gets that triple as a proposal
/// (<c>Status = proposed</c>, <c>Source = nt-form</c>, <c>UserId</c> NULL = system). Idempotent: a second run changes nothing.
/// </summary>
public sealed class LxxNtFormTagger
{
    private readonly ILxxNtFormRepository _repository;
    private readonly ILogger<LxxNtFormTagger> _logger;

    public LxxNtFormTagger(ILxxNtFormRepository repository, ILogger<LxxNtFormTagger> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<LxxPrefillResult> RunAsync(IProgress<string>? progress = null, CancellationToken token = default)
    {
        var watch = Stopwatch.StartNew();
        var analyses = await _repository.GetNtAnalysesAsync(token);
        var words = await _repository.GetLxxWordsAsync(token);
        var tags = await _repository.GetTagsAsync(token);

        var result = Compute(analyses, words, tags, out var proposals);
        var applied = await _repository.ApplyAsync(proposals, token);
        result = result with
        {
            Inserted = applied.Inserted,
            Updated = applied.Updated,
            Deleted = applied.Deleted,
            Duration = watch.Elapsed,
        };

        Report(result, progress);
        return result;
    }

    /// <summary>The pure part: classifies every word and collects the proposals. Public for the tests.</summary>
    public static LxxPrefillResult Compute(IEnumerable<NtFormAnalysis> analyses, IReadOnlyList<LxxPrefillWord> words, IReadOnlyList<LxxExistingTag> tags,
        out List<LxxNtFormProposal> proposals)
    {
        // normalized form -> the distinct analyses (null = more than one)
        var byForm = new Dictionary<string, (string? Lemma, string? Strongs, string? Morphology)?>(StringComparer.Ordinal);
        foreach (var a in analyses)
        {
            var form = LxxFormNormalizer.Normalize(a.Greek);
            if (form.Length == 0)
            {
                continue;
            }

            var triple = (Lemma: Clean(a.Lemma), Strongs: Clean(a.Strongs), Morphology: Clean(a.Morphology));
            if (!byForm.TryGetValue(form, out var known))
            {
                byForm[form] = triple;
            }
            else if (known is { } existing && existing != triple)
            {
                byForm[form] = null;
            }
        }

        var existingTags = tags.ToDictionary(t => t.LxxWordId);
        proposals = [];
        int ambiguous = 0, unknown = 0, capitals = 0, invalid = 0, empty = 0, kept = 0;
        var perBook = new Dictionary<(int Book, string Form), (int Words, int Proposed, int Covered)>();

        foreach (var word in words)
        {
            var proposed = false;
            var covered = false;
            if (existingTags.TryGetValue(word.Id, out var tag) && !(tag.Source == "nt-form" && tag.Status == "proposed"))
            {
                kept++;
                covered = true;
            }
            else if (LxxFormNormalizer.IsUnaccentedCapitals(word.Greek))
            {
                capitals++;
            }
            else
            {
                var form = LxxFormNormalizer.Normalize(word.Greek);
                if (form.Length == 0)
                {
                    empty++;
                }
                else if (!byForm.TryGetValue(form, out var triple))
                {
                    unknown++;
                }
                else if (triple is not { } t)
                {
                    ambiguous++;
                }
                else if (t.Lemma is null || t.Strongs is null || t.Morphology is null || !IsValidStrongs(t.Strongs) || !GreekMorphologyCode.IsValid(t.Morphology))
                {
                    invalid++;
                }
                else
                {
                    proposals.Add(new LxxNtFormProposal(word.Id, t.Lemma, t.Strongs, t.Morphology));
                    proposed = true;
                    covered = true;
                }
            }

            var key = (word.BibleBookId, word.TextForm);
            perBook.TryGetValue(key, out var counts);
            perBook[key] = (counts.Words + 1, counts.Proposed + (proposed ? 1 : 0), counts.Covered + (covered ? 1 : 0));
        }

        return new LxxPrefillResult
        {
            TotalWords = words.Count,
            Proposed = proposals.Count,
            Ambiguous = ambiguous,
            Unknown = unknown,
            SkippedCapitals = capitals,
            Invalid = invalid,
            Empty = empty,
            KeptExisting = kept,
            Covered = proposals.Count + kept,
            Books = perBook.OrderBy(p => p.Key.Book).ThenBy(p => p.Key.Form, StringComparer.Ordinal)
                .Select(p => new LxxPrefillBookCoverage(p.Key.Book, p.Key.Form, p.Value.Words, p.Value.Proposed, p.Value.Covered)).ToList(),
        };
    }

    /// <summary>The <c>LxxWordTags_Strongs_CK</c> format: <c>G</c>, four digits, optional disambiguation letter.</summary>
    public static bool IsValidStrongs(string strongs) =>
        strongs.Length is 5 or 6
        && strongs[0] == 'G'
        && strongs.AsSpan(1, 4).IndexOfAnyExceptInRange('0', '9') < 0
        && (strongs.Length == 5 || char.IsAsciiLetterUpper(strongs[5]));

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private void Report(LxxPrefillResult result, IProgress<string>? progress)
    {
        void Log(string line)
        {
            _logger.LogInformation("{Line}", line);
            progress?.Report(line);
        }

        foreach (var book in result.Books)
        {
            var name = GermanBookNames.GetAbbreviation(book.BibleBookId) + (book.TextForm.Length > 0 ? $" ({book.TextForm})" : string.Empty);
            Log($"{name}: {book.Covered} / {book.Words} words tagged ({Percent(book.Covered, book.Words)}), {book.Proposed} proposed");
        }

        Log($"Total: {result.Covered} / {result.TotalWords} words tagged ({Percent(result.Covered, result.TotalWords)}); {result.Proposed} pass-1 proposals, {result.KeptExisting} existing rows kept");
        Log($"Not tagged: {result.Ambiguous} ambiguous forms, {result.Unknown} unknown forms, {result.SkippedCapitals} unaccented capitals skipped, {result.Invalid} invalid Strong's/morphology, {result.Empty} empty");
        Log($"Written: {result.Inserted} inserted, {result.Updated} updated, {result.Deleted} deleted ({result.Changed} changed rows) in {result.Duration.TotalSeconds:F1} s");
    }

    private static string Percent(int part, int whole) => whole == 0 ? "0 %" : $"{100.0 * part / whole:F1} %";
}
