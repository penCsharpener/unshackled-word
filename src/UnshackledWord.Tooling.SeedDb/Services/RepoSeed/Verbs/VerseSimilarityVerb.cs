using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using UnshackledWord.Application.Features.VerseSimilarity;
using UnshackledWord.Domain.Models.BibleStructure.References;

namespace UnshackledWord.Tooling.SeedDb.Services.RepoSeed.Verbs;

/// <summary>
/// <c>verse-similarity --dry-run [--show &lt;ref&gt;]...</c> (roadmap 0027.1): loads the Strong's sequences of both testaments, scores all
/// verses and logs counts, stop list, duration and memory. <c>--show</c> (repeatable) prints the top 10 of a verse with the shared Strong's
/// and phrases (<c>--show-count n</c> for more than 10, capped by TopK). References: STEP/OSIS (<c>Heb.4.12</c>, <c>Act.7.48</c>; English counting = <c>LxxRefId</c>), the same dotted form with
/// German or English book names (<c>Mk.14.58</c>, <c>Ps.149.6</c>), or a German citation (<c>Mk 14,58</c>; Elberfelder counting, mapped
/// through <c>Elb1871Verses</c>). Optional overrides: <c>--stop-df-share</c>, <c>--top-k</c>, <c>--min-score</c>, <c>--phrase-weight</c>.
/// The dry run writes nothing.
/// <para>
/// Without <c>--dry-run</c> (roadmap 0027.2) it rebuilds <c>VerseSimilarities</c> through <see cref="VerseSimilarityRebuildService"/> (advisory lock,
/// run row, atomic replace) with the configured defaults (section <c>VerseSimilarity</c>) and the same overrides, and logs run id, pair count and
/// duration. Exit codes: 0 succeeded, 1 failed (old pairs kept), 2 bad arguments, 3 another rebuild is running.
/// </para>
/// </summary>
public sealed partial class VerseSimilarityVerb : ISeedVerb
{
    private const int DefaultShowCount = 10;
    private const int LastOtBookId = 39;

    private static readonly ScriptureParseOptions OsisOptions = new(AliasProfile.Osis, AllowPrefix: false, WholeInput: true, VerseCounting.English);
    private static readonly ScriptureParseOptions DottedOptions = new(AliasProfile.Legacy, AllowPrefix: false, WholeInput: true, VerseCounting.English);
    private static readonly ScriptureParseOptions GermanOptions = new(AliasProfile.Legacy, AllowPrefix: false, WholeInput: true, VerseCounting.Hebrew);

    private readonly IVerseTokenRepository _repository;
    private readonly VerseSimilarityRebuildService _rebuild;
    private readonly ILogger<VerseSimilarityVerb> _logger;

    public VerseSimilarityVerb(IVerseTokenRepository repository, VerseSimilarityRebuildService rebuild, ILogger<VerseSimilarityVerb> logger)
    {
        _repository = repository;
        _rebuild = rebuild;
        _logger = logger;
    }

    public string Name => "verse-similarity";

    public async Task<int> RunAsync(VerbArguments arguments, CancellationToken token)
    {
        if (!TryReadParameters(arguments, out var parameters))
        {
            return 2;
        }

        if (!arguments.Has("--dry-run"))
        {
            return await RebuildAsync(arguments, parameters, token);
        }

        var shows = new List<(string Input, int VerseId)>();
        foreach (var input in arguments.GetAll("--show"))
        {
            var verseId = await ResolveAsync(input, token);
            if (verseId is null)
            {
                _logger.LogError("Cannot read reference {Reference}. Examples: Heb.4.12, Mk.14.58, 'Mk 14,58'", input);
                return 2;
            }

            shows.Add((input, verseId.Value));
        }

        _logger.LogInformation("Verse similarity dry run: StopDfShare {StopDfShare}, TopK {TopK}, MinScore {MinScore}, PhraseWeight {PhraseWeight}",
            parameters.StopDfShare, parameters.TopK, parameters.MinScore, parameters.PhraseWeight);

        var showCount = int.TryParse(arguments.Get("--show-count"), NumberStyles.None, CultureInfo.InvariantCulture, out var count) && count > 0 ? count : DefaultShowCount;
        var total = Stopwatch.StartNew();
        var results = new Dictionary<Testament, (VerseSimilarityCorpus Corpus, ILookup<int, VerseSimilarityHit> Hits)>();

        foreach (var testament in new[] { Testament.Old, Testament.New })
        {
            results[testament] = await RunTestamentAsync(testament, parameters, arguments.Has("--base-strongs"), token);
        }

        using var process = Process.GetCurrentProcess();
        process.Refresh();
        _logger.LogInformation("Verse similarity dry run done in {Duration:F1} s; managed heap {Heap:F0} MB, peak working set {Peak:F0} MB",
            total.Elapsed.TotalSeconds, GC.GetTotalMemory(false) / 1048576.0, process.PeakWorkingSet64 / 1048576.0);

        foreach (var (input, verseId) in shows)
        {
            var testament = verseId / 1_000_000 <= LastOtBookId ? Testament.Old : Testament.New;
            var (corpus, hits) = results[testament];
            await ShowAsync(input, verseId, corpus, hits[verseId].Take(showCount).ToList(), token);
        }

        return 0;
    }

    private async Task<int> RebuildAsync(VerbArguments arguments, VerseSimilarityParameters parameters, CancellationToken token)
    {
        if (arguments.Has("--show") || arguments.Has("--base-strongs"))
        {
            _logger.LogWarning("--show and --base-strongs apply only with --dry-run; ignored for the rebuild");
        }

        var result = await _rebuild.RebuildAsync(parameters, startedBy: null, token);
        switch (result.Status)
        {
            case VerseSimilarityRebuildStatus.Succeeded:
                _logger.LogInformation("Verse similarity rebuild: run {RunId} succeeded, {Pairs} pairs, {Stops} stop words, {Duration:F1} s",
                    result.RunId, result.PairCount, result.StopWordCount, result.DurationMs / 1000.0);
                return 0;
            case VerseSimilarityRebuildStatus.Busy:
                _logger.LogError("Verse similarity rebuild: another rebuild is running (advisory lock held). Nothing was written");
                return 3;
            case VerseSimilarityRebuildStatus.Invalid:
                _logger.LogError("Verse similarity rebuild: {Error}. Nothing was written", result.Error);
                return 2;
            default:
                _logger.LogError("Verse similarity rebuild: run {RunId} failed after {Duration:F1} s: {Error}. The previous pairs stay",
                    result.RunId, result.DurationMs / 1000.0, result.Error);
                return 1;
        }
    }

    private async Task<(VerseSimilarityCorpus Corpus, ILookup<int, VerseSimilarityHit> Hits)> RunTestamentAsync(Testament testament, VerseSimilarityParameters parameters, bool baseStrongs, CancellationToken token)
    {
        var watch = Stopwatch.StartNew();
        var verses = await _repository.LoadAsync(testament, token);
        if (baseStrongs)
        {
            verses = verses.Select(v => v with { Strongs = v.Strongs.Select(BaseStrongs).ToList() }).ToList();
        }

        var loaded = watch.Elapsed;

        var corpus = VerseSimilarityScorer.BuildCorpus(verses, parameters);
        var built = watch.Elapsed;

        var hits = VerseSimilarityScorer.ScoreAll(corpus, token);
        var scored = watch.Elapsed;

        var stopList = corpus.StopList();
        var dfStops = stopList.Count(s => !VerseSimilarityScorer.IsPrefixStop(s.Strongs));
        var withHits = hits.Select(h => h.SourceVerseId).Distinct().Count();

        _logger.LogInformation(
            "{Testament}: {Verses} verses, {RawTokens} tokens ({Distinct} distinct Strong's), {ContentTokens} content tokens after the stop list; " +
            "stop words {StopCount} ({DfStops} by df > {Threshold:F0} verses, {PrefixStops} Hebrew prefixes)",
            testament, corpus.VerseCount, corpus.RawTokenCount, corpus.DistinctStrongsCount, corpus.ContentTokenCount,
            stopList.Count, dfStops, parameters.StopDfShare * corpus.VerseCount, stopList.Count - dfStops);
        _logger.LogInformation("{Testament} stop list (most frequent first): {StopList}",
            testament, string.Join(", ", stopList.Select(s => $"{s.Strongs}:{s.DocumentFrequency}")));
        _logger.LogInformation(
            "{Testament}: {Pairs} pairs for {WithHits} of {Verses} verses (avg {Avg:F1}); load {Load:F1} s, corpus {Build:F1} s, scoring {Score:F1} s; managed heap {Heap:F0} MB",
            testament, hits.Count, withHits, corpus.VerseCount, withHits == 0 ? 0 : (double)hits.Count / withHits,
            loaded.TotalSeconds, (built - loaded).TotalSeconds, (scored - built).TotalSeconds, GC.GetTotalMemory(false) / 1048576.0);

        return (corpus, hits.ToLookup(h => h.SourceVerseId));
    }

    private async Task ShowAsync(string input, int verseId, VerseSimilarityCorpus corpus, IReadOnlyList<VerseSimilarityHit> hits, CancellationToken token)
    {
        var builder = new StringBuilder();
        builder.Append(CultureInfo.InvariantCulture, $"Top {hits.Count} for {input} = {Format(verseId)} (LxxRefId {verseId})").AppendLine();

        if (!corpus.Contains(verseId))
        {
            builder.Append("  verse has no tokens in the STEP tables").AppendLine();
            _logger.LogInformation("{Show}", builder.ToString());
            return;
        }

        var explanations = hits.Select(h => VerseSimilarityScorer.Explain(corpus, verseId, h.TargetVerseId)!).ToList();
        var labels = await _repository.GetLabelsAsync(explanations.SelectMany(e => e.Shared.Select(s => s.Strongs)).ToList(), token);

        for (var i = 0; i < hits.Count; i++)
        {
            var hit = hits[i];
            var explanation = explanations[i];
            builder.Append(CultureInfo.InvariantCulture, $"  {hit.Rank,2}. {Format(hit.TargetVerseId),-14} {hit.Score:F3}  shared {hit.SharedCount}, phrases {hit.PhraseCount}").AppendLine();
            builder.Append("      shared: ")
                .AppendJoin(", ", explanation.Shared.Select(s => Label(s.Strongs, labels)))
                .AppendLine();

            if (explanation.Phrases.Count > 0)
            {
                builder.Append("      phrases: ")
                    .AppendJoin(" | ", explanation.Phrases.Select(p => string.Join(' ', p.Select(s => labels.TryGetValue(s, out var l) ? l.Lemma : s))))
                    .AppendLine();
            }
        }

        if (hits.Count == 0)
        {
            builder.Append("  no hits above MinScore").AppendLine();
        }

        _logger.LogInformation("{Show}", builder.ToString());
    }

    /// <summary>Diagnostic (<c>--base-strongs</c>, 07-risks.md): drops the disambiguation letter, <c>H0430G</c> → <c>H0430</c>.</summary>
    private static string BaseStrongs(string strongs) =>
        strongs.Length > 5 && char.IsAsciiLetter(strongs[^1]) ? strongs[..5] : strongs;

    private static string Label(string strongs, IReadOnlyDictionary<string, StrongsLabel> labels) =>
        labels.TryGetValue(strongs, out var label) ? $"{strongs} {label.Lemma} '{label.Gloss}'" : strongs;

    private static string Format(int refId)
    {
        var bookId = refId / 1_000_000;
        var chapter = refId / 1_000 % 1_000;
        var verse = refId % 1_000;
        return GermanReferenceFormatter.Format(new ScriptureReference(bookId, null, [new VerseSpan(chapter, verse, chapter, verse)], 0, 0, false));
    }

    private async Task<int?> ResolveAsync(string input, CancellationToken token)
    {
        if (SingleVerse(ScriptureReferenceParser.Parse(input, OsisOptions)) is { } osis)
        {
            return osis;
        }

        var dotted = DottedReference().Match(input.Trim());
        if (dotted.Success
            && SingleVerse(ScriptureReferenceParser.Parse($"{dotted.Groups[1].Value} {dotted.Groups[2].Value},{dotted.Groups[3].Value}", DottedOptions)) is { } english)
        {
            return english;
        }

        return SingleVerse(ScriptureReferenceParser.Parse(input, GermanOptions)) is { } hebRefId
            ? await _repository.FindLxxRefIdAsync(hebRefId, token)
            : null;
    }

    private static int? SingleVerse(ScriptureParseResult result) =>
        result is { IsSuccess: true, References: [{ BookId: { } bookId, BookOnly: false, Spans: [var span] }] }
        && span.StartVerse >= 0 && span.EndVerse != VerseSpan.WholeChapterEndVerse && span.StartChapter == span.EndChapter && span.StartVerse == span.EndVerse
            ? span.ToRefIds(bookId).StartRefId
            : null;

    private bool TryReadParameters(VerbArguments arguments, out VerseSimilarityParameters parameters)
    {
        var defaults = _rebuild.DefaultParameters;
        parameters = defaults;

        if (!TryRead(arguments, "--stop-df-share", defaults.StopDfShare, out var stopDfShare)
            || !TryRead(arguments, "--top-k", defaults.TopK, out var topK)
            || !TryRead(arguments, "--min-score", defaults.MinScore, out var minScore)
            || !TryRead(arguments, "--phrase-weight", defaults.PhraseWeight, out var phraseWeight))
        {
            return false;
        }

        parameters = new VerseSimilarityParameters(stopDfShare, (int)topK, minScore, phraseWeight);
        return true;
    }

    private bool TryRead(VerbArguments arguments, string option, double fallback, out double value)
    {
        value = fallback;
        var text = arguments.Get(option);
        if (text is null)
        {
            return true;
        }

        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && value >= 0)
        {
            return true;
        }

        _logger.LogError("{Option} needs a non-negative number, got {Value}", option, text);
        return false;
    }

    [GeneratedRegex(@"^(.+?)\.(\d+)\.(\d+)$")]
    private static partial Regex DottedReference();
}
