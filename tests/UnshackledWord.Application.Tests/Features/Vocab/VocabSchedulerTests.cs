using UnshackledWord.Application.Features.Vocab;

namespace UnshackledWord.Application.Tests.Features.Vocab;

public class VocabSchedulerTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
    private readonly VocabScheduler _scheduler = new();

    [Fact]
    public void New_card_has_strength_zero_and_is_due_now()
    {
        var card = _scheduler.NewCard(T0);
        card.State.ShouldBe(VocabCardState.New);
        card.Due.ShouldBe(T0);
        VocabScheduler.Strength(card).ShouldBe(0);
    }

    [Fact]
    public void New_card_graduates_after_one_correct_answer_with_single_10m_step()
    {
        var result = _scheduler.Review(_scheduler.NewCard(T0), true, T0);
        result.Card.State.ShouldBe(VocabCardState.Review);
        result.Card.Reps.ShouldBe(1);
        result.Card.Stability.ShouldBeGreaterThan(0);
        result.Card.Due.ShouldBeGreaterThanOrEqualTo(T0.AddDays(1));
    }

    [Fact]
    public void Wrong_answer_on_new_card_comes_back_in_10_minutes()
    {
        var result = _scheduler.Review(_scheduler.NewCard(T0), false, T0);
        result.Card.State.ShouldBe(VocabCardState.Learning);
        result.Card.Due.ShouldBe(T0.AddMinutes(10));
    }

    [Fact]
    public void Wrong_answer_on_review_card_goes_to_relearning_and_returns_in_10_minutes()
    {
        var learned = _scheduler.Review(_scheduler.NewCard(T0), true, T0).Card;
        var now = learned.Due;
        var result = _scheduler.Review(learned, false, now);
        result.Card.State.ShouldBe(VocabCardState.Relearning);
        result.Card.Due.ShouldBe(now.AddMinutes(10));
        result.Card.Lapses.ShouldBe(1);
        result.StabilityAfter.ShouldBeLessThan(result.StabilityBefore);
    }

    [Fact]
    public void Relearning_card_graduates_after_one_correct_answer()
    {
        var learned = _scheduler.Review(_scheduler.NewCard(T0), true, T0).Card;
        var lapsed = _scheduler.Review(learned, false, learned.Due).Card;
        var back = _scheduler.Review(lapsed, true, lapsed.Due).Card;
        back.State.ShouldBe(VocabCardState.Review);
    }

    [Fact]
    public void Stability_grows_across_days_of_correct_reviews()
    {
        var card = _scheduler.Review(_scheduler.NewCard(T0), true, T0).Card;
        var previous = card.Stability;
        for (var i = 0; i < 5; i++)
        {
            card = _scheduler.Review(card, true, card.Due).Card;
            card.Stability.ShouldBeGreaterThan(previous);
            previous = card.Stability;
        }
    }

    [Fact]
    public void Review_log_data_carries_before_and_after_values()
    {
        var first = _scheduler.Review(_scheduler.NewCard(T0), true, T0);
        first.StateBefore.ShouldBe(VocabCardState.New);
        first.RetrievabilityBefore.ShouldBeNull();
        var second = _scheduler.Review(first.Card, true, first.Card.Due);
        second.StabilityBefore.ShouldBe(first.StabilityAfter);
        second.DifficultyBefore.ShouldBe(first.DifficultyAfter);
        second.RetrievabilityBefore.ShouldNotBeNull();
    }

    [Fact]
    public void Retrievability_at_due_date_is_about_retention()
    {
        var card = _scheduler.Review(_scheduler.NewCard(T0), true, T0).Card;
        _scheduler.Retrievability(card, card.Due).ShouldBe(0.9, 0.02);
    }

    [Theory]
    [InlineData(0.5, 0)]
    [InlineData(1, 1)]
    [InlineData(3, 2)]
    [InlineData(7, 3)]
    [InlineData(21, 4)]
    [InlineData(60, 5)]
    [InlineData(500, 5)]
    public void Strength_follows_the_day_ladder(double stability, double expected) =>
        VocabScheduler.StrengthFromStability(stability).ShouldBe(expected);

    [Fact]
    public void Strength_interpolates_between_rungs() =>
        VocabScheduler.StrengthFromStability(2).ShouldBe(1.5);

    [Fact]
    public void Simulating_correct_reviews_for_30_days_reaches_strength_and_due_at_target_retrievability()
    {
        var card = _scheduler.Review(_scheduler.NewCard(T0), true, T0).Card;
        var end = T0.AddDays(30);
        var reviews = 1;
        while (card.Due <= end)
        {
            card = _scheduler.Review(card, true, card.Due).Card;
            reviews++;
        }

        Console.WriteLine($"30d: reviews={reviews} stability={card.Stability:F2} strength={VocabScheduler.Strength(card):F2} " +
                          $"dueInDays={(card.Due - card.LastReview!.Value).TotalDays:F1} r@due={_scheduler.Retrievability(card, card.Due):F3}");
        VocabScheduler.Strength(card).ShouldBeGreaterThan(3);
        _scheduler.Retrievability(card, card.Due).ShouldBe(0.9, 0.02);
    }

    [Fact]
    public void Daily_correct_reviews_for_30_days_report_stability()
    {
        var card = _scheduler.NewCard(T0);
        for (var day = 0; day <= 30; day++)
        {
            card = _scheduler.Review(card, true, T0.AddDays(day)).Card;
        }

        Console.WriteLine($"daily30: stability={card.Stability:F2} strength={VocabScheduler.Strength(card):F2} dueInDays={(card.Due - card.LastReview!.Value).TotalDays:F1}");
        card.Stability.ShouldBeGreaterThan(60);
        VocabScheduler.Strength(card).ShouldBe(5);
    }
}
