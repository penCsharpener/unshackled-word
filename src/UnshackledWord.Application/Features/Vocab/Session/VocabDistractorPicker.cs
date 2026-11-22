namespace UnshackledWord.Application.Features.Vocab.Session;

/// <summary>An entry that may serve as a wrong option; <paramref name="TopMeaning"/> is its top-ranked accepted meaning, the only German text ever shown for it.
/// <paramref name="AcceptedMeanings"/> (all accepted meanings) and <paramref name="Form"/> (dictionary form, else lemma: the text shown in reverse MC) are only needed for reverse MC.</summary>
public sealed record VocabDistractorCandidate(int EntryId, string? Pos, int Rank, string TopMeaning, IReadOnlyCollection<string>? AcceptedMeanings = null, string? Form = null);

/// <summary>The entry asked about: <paramref name="AcceptedMeanings"/> are all its accepted meanings (any of them is correct).</summary>
public sealed record VocabDistractorPrompt(int EntryId, string? Pos, int Rank, string CorrectText, IReadOnlyCollection<string> AcceptedMeanings);

/// <summary>
/// Picks the wrong German options of a multiple-choice question. The absolute rule (replaces Lexio's synset rule): never an option that is also
/// correct, i.e. a distractor's shown meaning is not an accepted meaning of the prompt entry (case-folded) and not the correct option itself.
/// Tiers, additive: 1) the same open wave, 2) the same POS and a rank within <see cref="NearbyRanks"/>, 3) the same POS, 4) anything in the course.
/// </summary>
public static class VocabDistractorPicker
{
    public const int NearbyRanks = 200;

    public static string Fold(string text) => text.Trim().ToLowerInvariant();

    public static List<VocabDistractorCandidate> Pick(
        VocabDistractorPrompt prompt,
        IReadOnlyCollection<VocabDistractorCandidate> pool,
        IReadOnlySet<int> openWave,
        int count,
        Random random)
    {
        var forbidden = prompt.AcceptedMeanings.Select(Fold).Append(Fold(prompt.CorrectText)).ToHashSet();

        return PickCore(prompt, pool, openWave, count, random, c => !forbidden.Contains(Fold(c.TopMeaning)), c => c.TopMeaning);
    }

    /// <summary>
    /// Reverse MC: the options are lemmas. The prompt is <see cref="VocabDistractorPrompt.CorrectText"/> (the top German meaning), the correct option
    /// shows <paramref name="correctForm"/>. A distractor must not have the prompt meaning among its accepted meanings (case-folded; λέγω and εἶπον
    /// both accept "sagen") and must not show the same form as the correct option or another distractor. Same tiers as <see cref="Pick"/>.
    /// </summary>
    public static List<VocabDistractorCandidate> PickLemmas(
        VocabDistractorPrompt prompt,
        string correctForm,
        IReadOnlyCollection<VocabDistractorCandidate> pool,
        IReadOnlySet<int> openWave,
        int count,
        Random random)
    {
        var meaning = Fold(prompt.CorrectText);
        var form = Fold(correctForm);

        return PickCore(
            prompt,
            pool,
            openWave,
            count,
            random,
            c => Fold(Shown(c)) != form && !(c.AcceptedMeanings ?? [c.TopMeaning]).Any(m => Fold(m) == meaning),
            Shown);
    }

    private static string Shown(VocabDistractorCandidate c) => c.Form ?? c.TopMeaning;

    private static List<VocabDistractorCandidate> PickCore(
        VocabDistractorPrompt prompt,
        IReadOnlyCollection<VocabDistractorCandidate> pool,
        IReadOnlySet<int> openWave,
        int count,
        Random random,
        Func<VocabDistractorCandidate, bool> allowed,
        Func<VocabDistractorCandidate, string> shown)
    {
        var picked = new List<VocabDistractorCandidate>();

        bool SamePos(VocabDistractorCandidate c) => string.Equals(c.Pos, prompt.Pos, StringComparison.Ordinal);

        Func<VocabDistractorCandidate, bool>[] tiers =
        [
            c => openWave.Contains(c.EntryId),
            c => SamePos(c) && Math.Abs(c.Rank - prompt.Rank) <= NearbyRanks,
            SamePos,
            _ => true,
        ];

        foreach (var tier in tiers)
        {
            if (picked.Count >= count)
            {
                break;
            }

            var eligible = pool
                .Where(c => c.EntryId != prompt.EntryId && tier(c) && allowed(c))
                .Where(c => picked.All(p => p.EntryId != c.EntryId))
                .OrderBy(_ => random.Next())
                .ToList();

            foreach (var candidate in eligible)
            {
                if (picked.Count >= count)
                {
                    break;
                }

                // two distractors with the same shown text would make two identical options
                if (picked.Any(p => Fold(shown(p)) == Fold(shown(candidate))))
                {
                    continue;
                }

                picked.Add(candidate);
            }
        }

        return picked;
    }
}
