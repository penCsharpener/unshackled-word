using System.Globalization;
using System.Text;
using System.Text.Json;
using UnshackledWord.Domain.Models.BibleStructure.References;

namespace UnshackledWord.Application.Features.VerseSimilarity;

/// <summary>
/// One raw TSK link (table <c>Tsk</c>, the unchanged <c>assets/tskxref.txt</c> import) in STEP numbering (<c>LxxRefId</c>, English counting):
/// <see cref="SourceVerseId"/> → <see cref="TargetStartVerseId"/>..<see cref="TargetEndVerseId"/> (null = one verse; a range never leaves its chapter).
/// </summary>
public readonly record struct TskLink(int SourceVerseId, int TargetStartVerseId, int? TargetEndVerseId);

/// <summary>A pair that must rank high (roadmap 0027, 03-algorithm.md "Evaluation"); both directions are reported.</summary>
public sealed record VerseSimilarityGoldenPair(int SourceVerseId, int TargetVerseId);

/// <summary>Recall of the raw TSK links of one testament (both ends in it) in the stored top lists.</summary>
/// <param name="Testament"><c>old</c> or <c>new</c>.</param>
/// <param name="Links">TSK links considered (distinct, within the testament, not pointing at the source itself).</param>
/// <param name="HitsAt10">Links whose target (any verse of a target range) is among the source's first 10 hits.</param>
/// <param name="HitsAt30">The same for the first 30 hits.</param>
public sealed record VerseSimilarityTestamentRecall(string Testament, int Links, int HitsAt10, int HitsAt30, double RecallAt10, double RecallAt30);

/// <summary>Rank of <see cref="TargetVerseId"/> in the hits of <see cref="SourceVerseId"/> and the reverse; null = not among the stored hits.</summary>
/// <param name="Source">German citation of the source (<c>Hebr 4,12</c>).</param>
/// <param name="Target">German citation of the target.</param>
public sealed record VerseSimilarityGoldenRank(string Source, int SourceVerseId, string Target, int TargetVerseId, int? Rank, int? ReverseRank);

/// <summary>
/// The evaluation stored as JSON in <c>VerseSimilarityRuns.Evaluation</c> (roadmap 0027.6). <see cref="Error"/> is set (and the lists empty)
/// when the evaluation itself failed; the run still succeeded.
/// </summary>
/// <param name="TopK">TopK of the run; recall@30 cannot exceed recall@TopK when TopK is below 30.</param>
public sealed record VerseSimilarityEvaluation(
    int TopK,
    IReadOnlyList<VerseSimilarityTestamentRecall> Testaments,
    IReadOnlyList<VerseSimilarityGoldenRank> GoldenPairs,
    string? Error = null)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static VerseSimilarityEvaluation Failed(int topK, string error) => new(topK, [], [], error);

    public string ToJson() => JsonSerializer.Serialize(this, Json);

    /// <summary>The evaluation of a run row, or null when the column is empty or not readable.</summary>
    public static VerseSimilarityEvaluation? FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<VerseSimilarityEvaluation>(json, Json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Plain-text lines for the SeedDb log.</summary>
    public string ToSummary()
    {
        var builder = new StringBuilder();
        if (Error is not null)
        {
            builder.Append("evaluation failed: ").Append(Error);
            return builder.ToString();
        }

        foreach (var t in Testaments)
        {
            builder.Append(CultureInfo.InvariantCulture,
                    $"  {t.Testament,-3} recall@30 {t.RecallAt30:P1} ({t.HitsAt30:N0} of {t.Links:N0} TSK links), recall@10 {t.RecallAt10:P1} ({t.HitsAt10:N0})")
                .AppendLine();
        }

        foreach (var g in GoldenPairs)
        {
            builder.Append(CultureInfo.InvariantCulture,
                    $"  {g.Source,-10} → {g.Target,-10} rank {Rank(g.Rank),-7}  reverse {Rank(g.ReverseRank)}")
                .AppendLine();
        }

        return builder.ToString().TrimEnd();
    }

    private static string Rank(int? rank) => rank?.ToString(CultureInfo.InvariantCulture) ?? "missing";
}

/// <summary>
/// Pure evaluation of a ranking against the raw TSK links and the golden pairs (roadmap 0027.6, 03-algorithm.md "Evaluation"). Everything is in
/// STEP numbering (<c>LxxRefId</c>): the pairs are stored that way and the raw TSK rows carry the same English counting, so no versification
/// mapping is involved.
/// </summary>
public static class VerseSimilarityEvaluator
{
    private const int LastOtBookId = 39;

    /// <summary>Heb 4:12 ↔ Rev 1:16 / 2:12; Mk 14:58 ↔ 2 Cor 5:1; Acts 7:48 ↔ Acts 17:24; Ps 149:6 ↔ Isa 41:15 (03-algorithm.md).</summary>
    public static IReadOnlyList<VerseSimilarityGoldenPair> GoldenPairs { get; } =
    [
        new(58_004_012, 66_001_016),
        new(58_004_012, 66_002_012),
        new(41_014_058, 47_005_001),
        new(44_007_048, 44_017_024),
        new(19_149_006, 23_041_015),
    ];

    public static Testament TestamentOf(int verseId) => verseId / 1_000_000 <= LastOtBookId ? Testament.Old : Testament.New;

    /// <param name="hits">All stored hits of the run (both testaments).</param>
    /// <param name="links">Raw TSK links; duplicates, cross-testament links and links whose target contains the source are ignored.</param>
    public static VerseSimilarityEvaluation Evaluate(IEnumerable<VerseSimilarityHit> hits, IEnumerable<TskLink> links, int topK,
        IReadOnlyList<VerseSimilarityGoldenPair>? goldenPairs = null)
    {
        var ranks = new Dictionary<int, Dictionary<int, int>>();
        foreach (var hit in hits)
        {
            if (!ranks.TryGetValue(hit.SourceVerseId, out var targets))
            {
                ranks[hit.SourceVerseId] = targets = [];
            }

            // Lowest rank wins if a target were ever listed twice.
            if (!targets.TryGetValue(hit.TargetVerseId, out var existing) || hit.Rank < existing)
            {
                targets[hit.TargetVerseId] = hit.Rank;
            }
        }

        var counts = new Dictionary<Testament, (int Links, int At10, int At30)> { [Testament.Old] = default, [Testament.New] = default };
        foreach (var link in links.Distinct())
        {
            var end = link.TargetEndVerseId ?? link.TargetStartVerseId;
            var start = Math.Min(link.TargetStartVerseId, end);
            end = Math.Max(link.TargetStartVerseId, end);
            var testament = TestamentOf(link.SourceVerseId);

            if (testament != TestamentOf(start) || testament != TestamentOf(end) || (link.SourceVerseId >= start && link.SourceVerseId <= end))
            {
                continue;
            }

            var best = BestRank(ranks, link.SourceVerseId, start, end);
            var (n, at10, at30) = counts[testament];
            counts[testament] = (n + 1, at10 + (best <= 10 ? 1 : 0), at30 + (best <= 30 ? 1 : 0));
        }

        var testaments = new[] { Testament.Old, Testament.New }
            .Select(t =>
            {
                var (n, at10, at30) = counts[t];
                return new VerseSimilarityTestamentRecall(t == Testament.Old ? "old" : "new", n, at10, at30, Share(at10, n), Share(at30, n));
            })
            .ToList();

        var golden = (goldenPairs ?? GoldenPairs)
            .Select(p => new VerseSimilarityGoldenRank(Label(p.SourceVerseId), p.SourceVerseId, Label(p.TargetVerseId), p.TargetVerseId,
                RankOf(ranks, p.SourceVerseId, p.TargetVerseId), RankOf(ranks, p.TargetVerseId, p.SourceVerseId)))
            .ToList();

        return new VerseSimilarityEvaluation(topK, testaments, golden);
    }

    /// <summary>German citation of a STEP verse id (<c>58004012</c> → <c>Hebr 4,12</c>).</summary>
    public static string Label(int verseId)
    {
        var bookId = verseId / 1_000_000;
        var chapter = verseId / 1_000 % 1_000;
        var verse = verseId % 1_000;
        return GermanReferenceFormatter.Format(new ScriptureReference(bookId, null, [new VerseSpan(chapter, verse, chapter, verse)], 0, 0, false));
    }

    private static int BestRank(Dictionary<int, Dictionary<int, int>> ranks, int source, int start, int end)
    {
        if (!ranks.TryGetValue(source, out var targets))
        {
            return int.MaxValue;
        }

        if (start == end)
        {
            return targets.GetValueOrDefault(start, int.MaxValue);
        }

        var best = int.MaxValue;
        foreach (var (target, rank) in targets)
        {
            if (target >= start && target <= end && rank < best)
            {
                best = rank;
            }
        }

        return best;
    }

    private static int? RankOf(Dictionary<int, Dictionary<int, int>> ranks, int source, int target) =>
        ranks.TryGetValue(source, out var targets) && targets.TryGetValue(target, out var rank) ? rank : null;

    private static double Share(int part, int whole) => whole == 0 ? 0 : Math.Round((double)part / whole, 4);
}
