using UnshackledWord.Application.Features.Vocab.Session;

namespace UnshackledWord.Application.Tests.Features.Vocab;

public class VocabQuizTypesTests
{
    private static readonly IReadOnlySet<string> None = new HashSet<string>();

    private static HashSet<string> Off(params string[] types) => types.ToHashSet();

    [Fact]
    public void Day_zero_asks_only_multiple_choice_first()
    {
        for (var seed = 0; seed < 30; seed++)
        {
            VocabQuizTypes.Candidates(0, None, new Random(seed)).First().ShouldBe("multiple_choice");
        }
    }

    [Fact]
    public void From_day_one_reverse_multiple_choice_can_come_first()
    {
        var firsts = Enumerable.Range(0, 60).Select(seed => VocabQuizTypes.Candidates(1, None, new Random(seed)).First()).ToHashSet();

        firsts.ShouldBe(["multiple_choice", "reverse_multiple_choice", "listening", "verse_context"], ignoreOrder: true);
    }

    [Fact]
    public void Disabled_reverse_multiple_choice_is_never_a_candidate()
    {
        for (var seed = 0; seed < 30; seed++)
        {
            VocabQuizTypes.Candidates(5, Off("reverse_multiple_choice"), new Random(seed)).ShouldBe(["multiple_choice", "listening", "verse_context", "typing"], ignoreOrder: true);
        }
    }

    [Fact]
    public void When_the_age_leaves_nothing_the_age_is_ignored()
    {
        // day 0 with multiple choice off: only reverse multiple choice is left, and it is allowed from day 1 only
        VocabQuizTypes.Candidates(0, Off("multiple_choice"), new Random(1)).ShouldBe(["reverse_multiple_choice", "listening", "verse_context", "typing"]);
    }

    [Fact]
    public void Fallback_lists_the_age_blocked_types_after_the_allowed_ones()
    {
        VocabQuizTypes.Candidates(0, None, new Random(1)).ShouldBe(["multiple_choice", "reverse_multiple_choice", "listening", "verse_context", "typing"]);
    }

    [Fact]
    public void Typing_is_allowed_from_day_two()
    {
        VocabQuizTypes.Candidates(1, None, new Random(1)).Take(4).ShouldNotContain("typing");
        Enumerable.Range(0, 60).Select(seed => VocabQuizTypes.Candidates(2, None, new Random(seed)).First()).ShouldContain("typing");
    }

    [Fact]
    public void Listening_and_verse_context_are_allowed_from_day_one_and_can_be_turned_off()
    {
        var day0 = Enumerable.Range(0, 30).SelectMany(seed => VocabQuizTypes.Candidates(0, None, new Random(seed)).Take(1)).ToHashSet();
        day0.ShouldBe(["multiple_choice"]);

        var day1 = Enumerable.Range(0, 80).Select(seed => VocabQuizTypes.Candidates(1, None, new Random(seed)).First()).ToHashSet();
        day1.ShouldContain("listening");
        day1.ShouldContain("verse_context");

        VocabQuizTypes.Candidates(5, Off("listening", "verse_context"), new Random(1)).ShouldNotContain("listening");
        VocabQuizTypes.Candidates(5, Off("listening", "verse_context"), new Random(1)).ShouldNotContain("verse_context");
        VocabQuizTypes.Candidates(5, Off("listening"), new Random(1)).ShouldContain("verse_context");
    }

    [Fact]
    public void Age_counts_whole_days_and_is_never_negative()
    {
        var now = new DateTime(2026, 3, 3, 8, 0, 0, DateTimeKind.Utc);

        VocabQuizTypes.AgeDays(now.AddHours(-23), now).ShouldBe(0);
        VocabQuizTypes.AgeDays(now.AddHours(-25), now).ShouldBe(1);
        VocabQuizTypes.AgeDays(now.AddDays(1), now).ShouldBe(0);
    }
}
