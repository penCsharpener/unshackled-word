namespace UnshackledWord.Application.Features.Vocab.Session;

/// <summary>
/// Numbers that define a study session (Lexio 04-study-engine): up to 20 words, introduced in waves of at most 4, three quizzes per word.
/// Reviews claim the target first, new words get what is left, clamped to 4–10.
/// </summary>
public static class VocabSessionRules
{
    public const int WordTarget = 20;
    public const int WaveSize = 4;
    public const int RepsPerWord = 3;
    public const int MinNewWords = 4;
    public const int MaxNewWords = 10;
    public const int OptionCount = 4;

    /// <summary>
    /// How many review words and how many new words a session takes. <paramref name="dueReviews"/> and <paramref name="newSupply"/> are what the
    /// course offers; the result never exceeds either.
    /// </summary>
    public static (int Reviews, int NewWords) Size(int dueReviews, int newSupply)
    {
        var target = Math.Min(WordTarget, dueReviews + newSupply);
        var minNew = Math.Min(MinNewWords, newSupply);
        var reviews = Math.Min(dueReviews, target - minNew);
        var newWords = Math.Clamp(target - reviews, minNew, Math.Min(MaxNewWords, newSupply));

        return (reviews, newWords);
    }
}

/// <summary>
/// The engine state of one session, stored as JSON in <c>VocabSessions.Plan</c>. Mutable on purpose (plain JSON bag); only
/// <see cref="VocabSessionPlanner"/> and the session service change it.
/// </summary>
public sealed class VocabSessionPlan
{
    public string CourseCode { get; set; } = string.Empty;
    public string Track { get; set; } = string.Empty;

    /// <summary>Review words first (lowest retrievability first), then the new words in rank order. The order is the tie-break of the picking.</summary>
    public List<VocabPlanWord> Words { get; set; } = [];

    /// <summary>Cards served so far (the timeline length); the next card gets this as its <see cref="VocabPendingCard.Seq"/>.</summary>
    public int Position { get; set; }

    /// <summary>Introductions of the current wave (reset when a new wave opens).</summary>
    public int WaveIntroduced { get; set; }

    /// <summary>The card served and not answered yet; repeated <c>next</c> calls return the same card.</summary>
    public VocabPendingCard? Pending { get; set; }
}

public sealed class VocabPlanWord
{
    public int EntryId { get; set; }
    public bool IsNew { get; set; }

    /// <summary>New word whose introduction card was served.</summary>
    public bool Introduced { get; set; }

    /// <summary>Quizzes answered this session, right or wrong.</summary>
    public int Reps { get; set; }

    public int Correct { get; set; }

    /// <summary>Timeline index of the last card (introduction or quiz) of this word; null when never shown.</summary>
    public int? LastShown { get; set; }

    /// <summary>Introduced and still <c>new</c>/<c>learning</c>: a member of the open wave, which must pass before the session ends.</summary>
    public bool InWave { get; set; }

    /// <summary>Retired or without an accepted meaning since the session started; never served.</summary>
    public bool Dropped { get; set; }

    public double StrengthStart { get; set; }
    public double StrengthNow { get; set; }
}

public sealed class VocabPendingCard
{
    public int Seq { get; set; }

    /// <summary><c>introduction</c> or <c>multiple_choice</c>.</summary>
    public string Kind { get; set; } = string.Empty;

    public int EntryId { get; set; }

    /// <summary>MC options in display order (the correct one is the option of <see cref="EntryId"/>). Never sent to the client with that mark.</summary>
    public List<VocabPendingOption> Options { get; set; } = [];
}

public sealed class VocabPendingOption
{
    public int EntryId { get; set; }
    public string Text { get; set; } = string.Empty;
}
