using FsrsSharp;
using FsrsSharp.Legacy;

namespace UnshackledWord.Application.Features.Vocab;

/// <summary>
/// The only class that references the FSRS package. Retention 0.9, learning and relearning steps <c>[10m]</c>, default v6 weights,
/// fuzz off (deterministic). Two verdicts only: wrong = Again, correct = Good. With a single 10m step a correct answer graduates a
/// new card straight to review (the package has no second step to go to); a wrong answer returns it in 10 minutes.
/// </summary>
public sealed class VocabScheduler
{
    /// <summary>Stability in days at which strength reaches 1, 2, 3, 4, 5 (Lexio's ladder). Below the first rung strength is 0.</summary>
    private static readonly double[] DayLadder = [1, 3, 7, 21, 60];

    private readonly Fsrs _fsrs = new(new FsrsParametersInput
    {
        RequestRetention = 0.9,
        LearningSteps = ["10m"],
        RelearningSteps = ["10m"],
        EnableShortTerm = true,
        EnableFuzz = false,
    });

    public VocabCard NewCard(DateTimeOffset now) =>
        new(VocabCardState.New, 0, 0, now, 0, 0, null);

    public VocabReviewResult Review(VocabCard card, bool correct, DateTimeOffset now)
    {
        var last = card.LastReview;
        // never let elapsed time go negative (clock skew, manual time travel)
        var effectiveNow = last is { } l && now < l ? l : now;
        var before = ToPackage(card);
        double? retrievability = card.State == VocabCardState.New ? null : _fsrs.Retrievability(before, effectiveNow.UtcDateTime);

        var next = _fsrs.Next(before, effectiveNow.UtcDateTime, correct ? Rating.Good : Rating.Again).Card;
        var after = FromPackage(next);
        return new VocabReviewResult(after, correct, card.State, card.Stability, card.Difficulty, after.Stability, after.Difficulty, retrievability);
    }

    /// <summary>Probability of recall in [0,1]; 0 for a card never reviewed.</summary>
    public double Retrievability(VocabCard card, DateTimeOffset now) =>
        card.LastReview is null ? 0 : _fsrs.Retrievability(ToPackage(card), now.UtcDateTime);

    /// <summary>Strength 0..5 from stability, interpolated between the rungs of the day ladder (1, 3, 7, 21, 60 days).</summary>
    public static double Strength(VocabCard card) => StrengthFromStability(card.Stability);

    public static double StrengthFromStability(double stability)
    {
        if (stability < DayLadder[0])
        {
            return 0;
        }

        for (var rung = 0; rung < DayLadder.Length - 1; rung++)
        {
            if (stability > DayLadder[rung + 1])
            {
                continue;
            }

            return rung + 1 + (stability - DayLadder[rung]) / (DayLadder[rung + 1] - DayLadder[rung]);
        }

        return 5;
    }

    private static Card ToPackage(VocabCard card) => new()
    {
        State = card.State switch
        {
            VocabCardState.New => State.New,
            VocabCardState.Learning => State.Learning,
            VocabCardState.Review => State.Review,
            _ => State.Relearning,
        },
        Stability = card.Stability,
        Difficulty = card.Difficulty,
        Due = card.Due.UtcDateTime,
        Reps = card.Reps,
        Lapses = card.Lapses,
        LastReview = card.LastReview?.UtcDateTime,
    };

    private static VocabCard FromPackage(Card card) => new(
        card.State switch
        {
            State.New => VocabCardState.New,
            State.Learning => VocabCardState.Learning,
            State.Review => VocabCardState.Review,
            _ => VocabCardState.Relearning,
        },
        card.Stability,
        card.Difficulty,
        new DateTimeOffset(DateTime.SpecifyKind(card.Due, DateTimeKind.Utc)),
        card.Reps,
        card.Lapses,
        card.LastReview is { } lr ? new DateTimeOffset(DateTime.SpecifyKind(lr, DateTimeKind.Utc)) : null);
}
