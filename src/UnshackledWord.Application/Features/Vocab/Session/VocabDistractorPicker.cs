namespace UnshackledWord.Application.Features.Vocab.Session;

/// <summary>An entry that may serve as a wrong option; <paramref name="TopMeaning"/> is its top-ranked accepted meaning, the only German text ever shown for it.</summary>
public sealed record VocabDistractorCandidate(int EntryId, string? Pos, int Rank, string TopMeaning);

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
                .Where(c => c.EntryId != prompt.EntryId && tier(c) && !forbidden.Contains(Fold(c.TopMeaning)))
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
                if (picked.Any(p => Fold(p.TopMeaning) == Fold(candidate.TopMeaning)))
                {
                    continue;
                }

                picked.Add(candidate);
            }
        }

        return picked;
    }
}
