namespace UnshackledWord.Application.Features.Vocab.Session;

public enum VocabSessionStepKind
{
    Introduce,
    Quiz,
    Done,
}

public sealed record VocabSessionStep(VocabSessionStepKind Kind, int EntryId);

/// <summary>One word the queue may serve next (Lexio's <c>QueueCandidate</c>).</summary>
public sealed record VocabQueueCandidate(int EntryId, bool IsWaveMember, int RepsThisSession, int? LastShownIndex, int PinnedPosition);

/// <summary>
/// The spacing ladder (Lexio): the card just served for a word schedules its next one this many cards later. Introduction +3, first quiz +8,
/// later quizzes +18. The floor is one card in between: never the same word twice in a row.
/// </summary>
public static class VocabSpacingLadder
{
    public const int MinimumGap = 1;

    public static int NextGap(int repsThisSession) => repsThisSession switch
    {
        0 => 3,
        1 => 8,
        _ => 18,
    };
}

/// <summary>
/// Picking inside a session (Lexio "the order the engine picks in"). 1) an introduction while the wave has room (see
/// <see cref="VocabSessionPlanner.Next"/>), 2) a repetition that is due, most overdue first (open wave wins ties), 3) a word not yet shown,
/// in pinned order (the spacers), 4) a repetition served early, honouring only the floor.
/// </summary>
public static class VocabSessionQueue
{
    public static int? PickNext(IReadOnlyList<VocabQueueCandidate> candidates, int position)
    {
        var shown = candidates
            .Where(c => c.LastShownIndex is not null)
            .Select(c => (Candidate: c, Gap: VocabSpacingLadder.NextGap(c.RepsThisSession), Since: position - 1 - c.LastShownIndex!.Value))
            .ToList();

        var due = MostOverdue(shown, s => s.Since >= s.Gap);

        if (due is not null)
        {
            return due;
        }

        var neverShown = candidates
            .Where(c => c.LastShownIndex is null)
            .OrderBy(c => c.PinnedPosition)
            .FirstOrDefault();

        if (neverShown is not null)
        {
            return neverShown.EntryId;
        }

        return MostOverdue(shown, s => s.Since >= VocabSpacingLadder.MinimumGap);
    }

    private static int? MostOverdue(
        List<(VocabQueueCandidate Candidate, int Gap, int Since)> shown,
        Func<(VocabQueueCandidate Candidate, int Gap, int Since), bool> eligible) =>
        shown
            .Where(eligible)
            .OrderByDescending(s => s.Since - s.Gap)
            .ThenByDescending(s => s.Candidate.IsWaveMember)
            .ThenBy(s => s.Candidate.PinnedPosition)
            .Select(s => (int?)s.Candidate.EntryId)
            .FirstOrDefault();
}

/// <summary>The session state machine: what is served next, and what changes when a card is served or answered. Pure, no I/O.</summary>
public static class VocabSessionPlanner
{
    /// <summary>
    /// The next step. An introduction comes first whenever no wave is open (so a new wave of up to four starts) or the current wave is still
    /// being introduced and has room; otherwise the queue picks. A member of the open wave must pass before the session ends, so it is served
    /// even when it was the last card (a back-to-back repeat is the lesser evil than ending with an open wave), and it stays eligible
    /// beyond three quizzes until its card leaves <c>new</c>/<c>learning</c>. The session ends when nothing is left.
    /// </summary>
    public static VocabSessionStep Next(VocabSessionPlan plan)
    {
        var active = plan.Words.Where(w => !w.Dropped).ToList();
        var inWave = active.Count(w => w.InWave);
        var pendingNew = active.FirstOrDefault(w => w.IsNew && !w.Introduced);

        // reviews first: every review word of the session is asked once before the first introduction
        var reviewsPending = active.Any(w => !w.IsNew && w.LastShown is null);

        if (pendingNew is not null && !reviewsPending && (inWave == 0 || plan.WaveIntroduced < VocabSessionRules.WaveSize))
        {
            return new VocabSessionStep(VocabSessionStepKind.Introduce, pendingNew.EntryId);
        }

        var candidates = active
            .Select((w, index) => (Word: w, Index: index))
            .Where(x => (!x.Word.IsNew || x.Word.Introduced) && (x.Word.Reps < VocabSessionRules.RepsPerWord || x.Word.InWave))
            .Select(x => new VocabQueueCandidate(x.Word.EntryId, x.Word.InWave, x.Word.Reps, x.Word.LastShown, x.Index))
            .ToList();

        var pick = VocabSessionQueue.PickNext(candidates, plan.Position);

        if (pick is not null)
        {
            return new VocabSessionStep(VocabSessionStepKind.Quiz, pick.Value);
        }

        // nothing clears the floor: an open wave member still has to pass
        var forced = candidates.Where(c => c.IsWaveMember).OrderBy(c => c.LastShownIndex ?? -1).ThenBy(c => c.PinnedPosition).FirstOrDefault();

        return forced is not null
            ? new VocabSessionStep(VocabSessionStepKind.Quiz, forced.EntryId)
            : new VocabSessionStep(VocabSessionStepKind.Done, 0);
    }

    /// <summary>Records a served card on the timeline: position, the word's last-shown index and, for an introduction, the wave bookkeeping.</summary>
    public static VocabPendingCard Serve(VocabSessionPlan plan, VocabSessionStep step, string kind, List<VocabPendingOption>? options = null)
    {
        var word = plan.Words.Single(w => w.EntryId == step.EntryId);
        var seq = plan.Position;

        if (kind == "introduction")
        {
            if (!plan.Words.Any(w => w.InWave && !w.Dropped))
            {
                plan.WaveIntroduced = 0;
            }

            plan.WaveIntroduced++;
            word.Introduced = true;
            word.InWave = true;
        }

        word.LastShown = seq;
        plan.Position++;
        plan.Pending = new VocabPendingCard { Seq = seq, Kind = kind, EntryId = step.EntryId, Options = options ?? [] };

        return plan.Pending;
    }

    /// <summary>Records an answered quiz: reps, and the word leaves the open wave once its card is no longer <c>new</c>/<c>learning</c>.</summary>
    public static void Answered(VocabSessionPlan plan, int entryId, bool correct, VocabCardState stateAfter, double strengthAfter)
    {
        var word = plan.Words.Single(w => w.EntryId == entryId);
        word.Reps++;

        if (correct)
        {
            word.Correct++;
        }

        word.StrengthNow = strengthAfter;

        if (stateAfter is VocabCardState.Review or VocabCardState.Relearning)
        {
            word.InWave = false;
        }

        plan.Pending = null;
    }

    /// <summary>Takes a word out of the session (retired or no accepted meaning any more).</summary>
    public static void Drop(VocabSessionPlan plan, int entryId)
    {
        var word = plan.Words.Single(w => w.EntryId == entryId);
        word.Dropped = true;
        word.InWave = false;
    }
}
