namespace UnshackledWord.Application.Features.Vocab;

/// <summary>State of a vocab card; the lowercase names are what <c>VocabCards.State</c> stores.</summary>
public enum VocabCardState
{
    New,
    Learning,
    Review,
    Relearning,
}

/// <summary>Scheduling state of one card, matching the <c>VocabCards</c> columns. Callers never see FSRS package types.</summary>
public sealed record VocabCard(
    VocabCardState State,
    double Stability,
    double Difficulty,
    DateTimeOffset Due,
    int Reps,
    int Lapses,
    DateTimeOffset? LastReview)
{
    public string StateName => State.ToString().ToLowerInvariant();

    public static VocabCardState ParseState(string name) =>
        Enum.TryParse<VocabCardState>(name, ignoreCase: true, out var state)
            ? state
            : throw new ArgumentException($"Unknown card state '{name}'.", nameof(name));
}

/// <summary>One answer as the review log stores it (before/after memory values).</summary>
public sealed record VocabReviewResult(
    VocabCard Card,
    bool Correct,
    VocabCardState StateBefore,
    double StabilityBefore,
    double DifficultyBefore,
    double StabilityAfter,
    double DifficultyAfter,
    double? RetrievabilityBefore);
