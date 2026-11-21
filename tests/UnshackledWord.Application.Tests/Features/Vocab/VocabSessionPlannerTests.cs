using UnshackledWord.Application.Features.Vocab;
using UnshackledWord.Application.Features.Vocab.Session;

namespace UnshackledWord.Application.Tests.Features.Vocab;

public class VocabSessionPlannerTests
{
    /// <summary>Review words 101.. first, then new words 1.. (ranks), like the plan the service builds.</summary>
    private static VocabSessionPlan PlanOf(int reviews, int newWords)
    {
        var plan = new VocabSessionPlan { CourseCode = "nt-greek", Track = "G" };
        plan.Words.AddRange(Enumerable.Range(101, reviews).Select(id => new VocabPlanWord { EntryId = id }));
        plan.Words.AddRange(Enumerable.Range(1, newWords).Select(id => new VocabPlanWord { EntryId = id, IsNew = true }));

        return plan;
    }

    /// <summary>Runs the session to its end. <paramref name="correct"/> decides each quiz (entry, reps already done); card states follow single-step FSRS.</summary>
    private static List<(string Kind, int EntryId)> Run(VocabSessionPlan plan, Func<int, int, bool>? correct = null, int maxCards = 500)
    {
        correct ??= (_, _) => true;
        var states = plan.Words.ToDictionary(w => w.EntryId, w => w.IsNew ? VocabCardState.New : VocabCardState.Review);
        var served = new List<(string, int)>();

        while (served.Count < maxCards)
        {
            var step = VocabSessionPlanner.Next(plan);

            if (step.Kind == VocabSessionStepKind.Done)
            {
                break;
            }

            if (step.Kind == VocabSessionStepKind.Introduce)
            {
                VocabSessionPlanner.Serve(plan, step, "introduction");
                served.Add(("introduction", step.EntryId));
                plan.Pending = null;
                states[step.EntryId] = VocabCardState.New;

                continue;
            }

            VocabSessionPlanner.Serve(plan, step, "multiple_choice");
            served.Add(("multiple_choice", step.EntryId));
            var word = plan.Words.Single(w => w.EntryId == step.EntryId);
            var ok = correct(step.EntryId, word.Reps);
            states[step.EntryId] = (states[step.EntryId], ok) switch
            {
                (VocabCardState.New, true) => VocabCardState.Review,
                (VocabCardState.New, false) => VocabCardState.Learning,
                (VocabCardState.Learning, true) => VocabCardState.Review,
                (VocabCardState.Learning, false) => VocabCardState.Learning,
                (_, true) => VocabCardState.Review,
                _ => VocabCardState.Relearning,
            };
            VocabSessionPlanner.Answered(plan, step.EntryId, ok, states[step.EntryId], 0);
        }

        return served;
    }

    [Fact]
    public void Size_reviews_take_the_target_first_new_words_get_the_rest_clamped_4_to_10()
    {
        VocabSessionRules.Size(0, 5000).ShouldBe((0, 10));
        VocabSessionRules.Size(4, 5000).ShouldBe((4, 10));
        VocabSessionRules.Size(14, 5000).ShouldBe((14, 6));
        VocabSessionRules.Size(30, 5000).ShouldBe((16, 4));
        VocabSessionRules.Size(0, 3).ShouldBe((0, 3));
        VocabSessionRules.Size(3, 0).ShouldBe((3, 0));
    }

    [Fact]
    public void Fresh_session_introduces_a_wave_of_four_in_rank_order_before_the_first_question()
    {
        var served = Run(PlanOf(0, 10));

        served.Take(4).ShouldBe([("introduction", 1), ("introduction", 2), ("introduction", 3), ("introduction", 4)]);
        served[4].Kind.ShouldBe("multiple_choice");
        // the first question comes three cards after its introduction at the earliest (the ladder: +3)
        served.Take(4).Select(x => x.EntryId).ShouldBe([1, 2, 3, 4]);
    }

    [Fact]
    public void Every_word_gets_three_quizzes_and_the_session_ends_with_no_open_wave()
    {
        var plan = PlanOf(0, 10);
        var served = Run(plan);

        foreach (var id in Enumerable.Range(1, 10))
        {
            served.Count(x => x.EntryId == id && x.Kind == "multiple_choice").ShouldBe(3);
            served.Count(x => x.EntryId == id && x.Kind == "introduction").ShouldBe(1);
        }

        plan.Words.Any(w => w.InWave).ShouldBeFalse();
        VocabSessionPlanner.Next(plan).Kind.ShouldBe(VocabSessionStepKind.Done);
    }

    [Fact]
    public void The_next_wave_opens_only_when_the_open_wave_has_passed()
    {
        var served = Run(PlanOf(0, 10));
        var firstOfSecondWave = served.FindIndex(x => x.Kind == "introduction" && x.EntryId == 5);

        // by then the first four have each been answered once and left the open wave (single learning step), and no later introduction precedes it
        served.Take(firstOfSecondWave).Where(x => x.Kind == "introduction").Select(x => x.EntryId).ShouldBe([1, 2, 3, 4]);
        foreach (var id in new[] { 1, 2, 3, 4 })
        {
            served.Take(firstOfSecondWave).Count(x => x.EntryId == id && x.Kind == "multiple_choice").ShouldBeGreaterThanOrEqualTo(1);
        }

        // waves are 4, 4, 2
        var introductions = served.Where(x => x.Kind == "introduction").Select(x => x.EntryId).ToList();
        introductions.ShouldBe(Enumerable.Range(1, 10).ToList());
    }

    [Fact]
    public void A_wave_member_that_keeps_failing_holds_the_session_open_beyond_three_quizzes()
    {
        var plan = PlanOf(0, 4);
        var served = Run(plan, (id, reps) => !(id == 3 && reps < 5));

        // word 3 is wrong five times, so it is quizzed six times: until its card leaves learning
        served.Count(x => x.EntryId == 3 && x.Kind == "multiple_choice").ShouldBe(6);
        plan.Words.Single(w => w.EntryId == 3).InWave.ShouldBeFalse();
        VocabSessionPlanner.Next(plan).Kind.ShouldBe(VocabSessionStepKind.Done);
    }

    [Fact]
    public void A_lapsed_review_word_does_not_hold_the_session_open()
    {
        var plan = PlanOf(3, 0);
        var served = Run(plan, (_, _) => false);

        served.Count.ShouldBe(9);
        plan.Words.All(w => !w.InWave).ShouldBeTrue();
    }

    [Fact]
    public void Next_wave_waits_for_the_open_wave_even_when_a_review_spacer_is_available()
    {
        var plan = PlanOf(5, 6);
        var served = Run(plan);
        var secondWaveStart = served.FindIndex(x => x.Kind == "introduction" && x.EntryId == 5);

        served.Take(secondWaveStart).Count(x => x.Kind == "introduction").ShouldBe(4);
        served.Take(secondWaveStart).Where(x => x.EntryId is >= 1 and <= 4).Count(x => x.Kind == "multiple_choice").ShouldBeGreaterThanOrEqualTo(4);
    }

    [Fact]
    public void Review_words_follow_the_lexio_stream_nine_words_before_any_repeats()
    {
        var served = Run(PlanOf(16, 0)).Select(x => x.EntryId - 100).ToList();

        // Lexio 04-study-engine: 1..9, 1..16 ... (nine different words before the first repeat)
        served.Take(9).ShouldBe(Enumerable.Range(1, 9).ToList());
        served[9].ShouldBe(1);
        served.Count.ShouldBe(48);
        served.GroupBy(x => x).All(g => g.Count() == 3).ShouldBeTrue();
    }

    [Fact]
    public void Never_the_same_word_twice_in_a_row_while_another_candidate_exists()
    {
        var served = Run(PlanOf(6, 4));

        for (var i = 1; i < served.Count; i++)
        {
            (served[i].EntryId == served[i - 1].EntryId).ShouldBeFalse($"card {i} repeats {served[i].EntryId}");
        }
    }

    [Fact]
    public void A_dropped_word_is_never_served_and_a_dropped_wave_member_does_not_hold_the_session()
    {
        var plan = PlanOf(0, 4);
        VocabSessionPlanner.Drop(plan, 2);
        var served = Run(plan);

        served.Any(x => x.EntryId == 2).ShouldBeFalse();
        served.Count(x => x.Kind == "introduction").ShouldBe(3);
    }

    [Fact]
    public void Pending_new_words_with_an_empty_allowance_do_not_exist_the_session_just_ends()
    {
        var plan = PlanOf(0, 0);

        VocabSessionPlanner.Next(plan).Kind.ShouldBe(VocabSessionStepKind.Done);
    }

    [Fact]
    public void Queue_picks_most_overdue_due_repetition_and_open_wave_wins_a_tie()
    {
        // position 20: word 1 shown at 5 (gap after introduction 3 -> overdue by 11), word 2 shown at 5 as a wave member, same overdue
        var candidates = new List<VocabQueueCandidate>
        {
            new(1, false, 0, 5, 0),
            new(2, true, 0, 5, 1),
        };

        VocabSessionQueue.PickNext(candidates, 20).ShouldBe(2);
    }

    [Fact]
    public void Reviews_are_asked_once_each_before_the_first_introduction()
    {
        var served = Run(PlanOf(3, 4));

        served.Take(3).ShouldAllBe(x => x.Kind == "multiple_choice" && x.EntryId > 100);
        served.Take(3).Select(x => x.EntryId).Distinct().Count().ShouldBe(3);
        served[3].ShouldBe(("introduction", 1));
    }
}
