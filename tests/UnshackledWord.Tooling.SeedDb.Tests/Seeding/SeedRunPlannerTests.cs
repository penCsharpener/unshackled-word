using UnshackledWord.Domain.WebApi.Admin;
using UnshackledWord.Seeding.Catalog;

namespace UnshackledWord.Tooling.SeedDb.Tests.Seeding;

public class SeedRunPlannerTests
{
    private static readonly IReadOnlySet<string> None = new HashSet<string>();

    /// <summary>States of the real catalog; every table of a step in <paramref name="filled"/> has rows, the rest is empty.</summary>
    private static List<SetupStepResponse> States(params string[] filled)
    {
        var counts = SeedCatalog.Steps
            .Where(s => filled.Contains(s.Key))
            .SelectMany(s => s.Tables)
            .ToDictionary(t => t.Name, t => new TableCount(t.Name, 5));
        return SetupStatusCalculator.Compute(SeedCatalog.Steps, counts, None, None);
    }

    private static SeedRunPlan Plan(List<SetupStepResponse> states, SetupRunRequest request) => SeedRunPlanner.Plan(SeedCatalog.Steps, states, request);

    [Fact]
    public void A_red_step_is_planned_with_its_key_as_kind()
    {
        var plan = Plan(States("books"), new SetupRunRequest { Keys = ["elb1871-text"] });
        plan.IsValid.ShouldBeTrue();
        plan.Keys.ShouldBe(["elb1871-text"]);
        plan.Kind.ShouldBe("elb1871-text");
    }

    [Fact]
    public void A_green_step_is_rejected_as_not_red()
    {
        var plan = Plan(States("books", "elb1871-text"), new SetupRunRequest { Keys = ["elb1871-text"] });
        plan.IsValid.ShouldBeFalse();
        plan.Error.ShouldContain("not red");
    }

    [Fact]
    public void A_step_behind_a_missing_prerequisite_is_rejected_unless_the_plan_brings_it()
    {
        var states = States("books");
        var alone = Plan(states, new SetupRunRequest { Keys = ["elb1871-verses"] });
        alone.IsValid.ShouldBeFalse();
        alone.Error.ShouldContain("elb1871-text");

        var both = Plan(states, new SetupRunRequest { Keys = ["elb1871-verses", "elb1871-text"] });
        both.Keys.ShouldBe(["elb1871-text", "elb1871-verses"], "catalog order, not request order");
    }

    [Fact]
    public void An_unwired_or_read_only_step_is_rejected()
    {
        Plan(States("books"), new SetupRunRequest { Keys = ["morphology"] }).Error.ShouldContain("not wired");
        Plan(States(), new SetupRunRequest { Keys = ["books"] }).Error.ShouldContain("read-only");
        Plan(States(), new SetupRunRequest { Keys = ["nope"] }).Error.ShouldContain("Unknown");
    }

    [Fact]
    public void An_amber_step_runs_only_when_it_is_resumable()
    {
        // verses has one table; make elb1871-text amber (words filled, counting mapping empty): not resumable.
        var counts = new Dictionary<string, TableCount> { ["Elb1871Words"] = new("Elb1871Words", 5), ["BibleBooks"] = new("BibleBooks", 66), ["Languages"] = new("Languages", 2) };
        var states = SetupStatusCalculator.Compute(SeedCatalog.Steps, counts, None, None);
        states.Single(s => s.Key == "elb1871-text").Status.ShouldBe(SetupStepStatuses.Amber);
        Plan(states, new SetupRunRequest { Keys = ["elb1871-text"] }).Error.ShouldContain("not red");
    }

    [Fact]
    public void The_base_group_on_an_empty_database_runs_all_red_steps_in_order()
    {
        var plan = Plan(States("books"), new SetupRunRequest { Group = SetupGroups.Base });
        plan.Keys.ShouldBe(["stts", "revision-levels", "elb1871-text", "elb1871-verses"]);
        plan.Kind.ShouldBe("group:base");
    }

    [Fact]
    public void All_skips_green_steps_and_unwired_groups()
    {
        var plan = Plan(States("books", "stts"), new SetupRunRequest { All = true });
        plan.Keys.ShouldBe(["revision-levels", "elb1871-text", "elb1871-verses", "step-text", "step-strongs", "step-morphology", "step-lexicon", "step-relationship", "step-strongs-text"]);
        plan.Kind.ShouldBe("all");
    }

    [Fact]
    public void The_step_group_runs_all_six_steps_in_order_and_skips_what_waits_on_a_red_base_step()
    {
        var ready = Plan(States("books", "elb1871-text"), new SetupRunRequest { Group = SetupGroups.Step });
        ready.Keys.ShouldBe(["step-text", "step-strongs", "step-morphology", "step-lexicon", "step-relationship", "step-strongs-text"]);
        ready.Kind.ShouldBe("group:step");

        // without the Elb1871 text only step-strongs-text is left out
        Plan(States("books"), new SetupRunRequest { Group = SetupGroups.Step }).Keys
            .ShouldBe(["step-text", "step-strongs", "step-morphology", "step-lexicon", "step-relationship"]);

        // second run of the group: everything is green, rejected
        var done = Plan(States("books", "elb1871-text", "step-text", "step-strongs", "step-morphology", "step-lexicon", "step-relationship", "step-strongs-text"),
            new SetupRunRequest { Group = SetupGroups.Step });
        done.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void A_group_with_nothing_red_is_rejected()
    {
        Plan(States("books", "stts", "revision-levels", "elb1871-text", "elb1871-verses"), new SetupRunRequest { Group = SetupGroups.Base }).IsValid.ShouldBeFalse();
        Plan(States("books"), new SetupRunRequest { Group = SetupGroups.Other }).IsValid.ShouldBeFalse();
        Plan(States("books"), new SetupRunRequest { Group = "x" }).Error.ShouldContain("Unknown group");
    }

    [Fact]
    public void Exactly_one_of_keys_group_all_is_required()
    {
        var states = States("books");
        Plan(states, new SetupRunRequest()).IsValid.ShouldBeFalse();
        Plan(states, new SetupRunRequest { All = true, Group = SetupGroups.Base }).IsValid.ShouldBeFalse();
    }
}
