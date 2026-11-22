namespace UnshackledWord.Application.Features.Vocab.Session;

/// <summary>A quiz type and the word age (days since the card was created) it is first allowed at.</summary>
public sealed record VocabQuizRung(string Type, int FromDay);

/// <summary>
/// The quiz ladder (roadmap 0022.9, Lexio): which quiz types exist, from which word age each is allowed and which of them the learner may turn off.
/// Types the engine does not build yet are left out of <see cref="Implemented"/>; later rows (typing, listening, verse context) add themselves
/// there and give the service a builder, nothing else changes.
/// </summary>
public static class VocabQuizTypes
{
    public const string Introduction = "introduction";
    public const string MultipleChoice = "multiple_choice";
    public const string ReverseMultipleChoice = "reverse_multiple_choice";
    public const string Listening = "listening";
    public const string VerseContext = "verse_context";
    public const string Typing = "typing";

    /// <summary>What the learner may turn off (typing always works, the introduction is no quiz).</summary>
    public static readonly IReadOnlyList<string> Toggleable = [MultipleChoice, ReverseMultipleChoice, Listening, VerseContext];

    /// <summary>The two multiple-choice types; at least one of them stays on.</summary>
    public static readonly IReadOnlyList<string> MultipleChoiceTypes = [MultipleChoice, ReverseMultipleChoice];

    public static readonly IReadOnlyList<VocabQuizRung> Ladder =
    [
        new(MultipleChoice, 0),
        new(ReverseMultipleChoice, 1),
        new(Listening, 1),
        new(VerseContext, 1),
        new(Typing, 2),
    ];

    /// <summary>The types the engine can build at the moment. Extend when a later row implements one.</summary>
    public static readonly IReadOnlyList<string> Implemented = [MultipleChoice, ReverseMultipleChoice];

    /// <summary>Whole days between the card's creation and now (never negative).</summary>
    public static int AgeDays(DateTime createdAtUtc, DateTime nowUtc) => Math.Max(0, (int)Math.Floor((nowUtc - createdAtUtc).TotalDays));

    /// <summary>
    /// The order in which the service tries the types of one question. First the enabled, implemented types the word's age allows, in random
    /// order; then, as the fallback ("if a type is not possible, take the next one"; "if the age leaves nothing, ignore the age"), the other
    /// enabled types by their lowest age. The service takes the first one it can build.
    /// </summary>
    public static List<string> Candidates(int ageDays, IReadOnlySet<string> disabled, Random random)
    {
        var enabled = Ladder.Where(r => Implemented.Contains(r.Type) && !disabled.Contains(r.Type)).ToList();
        var byAge = enabled.Where(r => r.FromDay <= ageDays).Select(r => r.Type).OrderBy(_ => random.Next()).ToList();
        var rest = enabled.Where(r => r.FromDay > ageDays).OrderBy(r => r.FromDay).Select(r => r.Type);

        return byAge.Concat(rest).ToList();
    }
}
