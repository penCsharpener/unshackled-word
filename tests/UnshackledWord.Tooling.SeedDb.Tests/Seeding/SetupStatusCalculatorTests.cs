using UnshackledWord.Domain.WebApi.Admin;
using UnshackledWord.Seeding.Catalog;

namespace UnshackledWord.Tooling.SeedDb.Tests.Seeding;

public class SetupStatusCalculatorTests
{
    private static readonly IReadOnlySet<string> None = new HashSet<string>();

    private static SeedStep Step(string key, string[] requires, string[] tables, string? note = null)
        => new(key, SetupGroups.Base, SetupSources.Assets, null, null, [.. tables.Select(t => new SeedTable(t))], requires, true, true, true, note);

    private static Dictionary<string, TableCount> Counts(params (string Name, long Rows)[] rows)
        => rows.ToDictionary(r => r.Name, r => new TableCount(r.Name, r.Rows));

    [Fact]
    public void Green_amber_red_and_grey_follow_the_counts()
    {
        SeedStep[] catalog =
        [
            Step("a", [], ["A1", "A2"]),
            Step("b", [], ["B1", "B2"]),
            Step("c", [], ["C1"]),
            Step("d", ["c"], ["D1"]),
        ];

        var steps = SetupStatusCalculator.Compute(catalog, Counts(("A1", 5), ("A2", 1), ("B1", 3)), None, None);

        steps.Select(s => s.Status).ShouldBe([SetupStepStatuses.Green, SetupStepStatuses.Amber, SetupStepStatuses.Red, SetupStepStatuses.Grey]);
        steps[3].Missing.ShouldBe(["c"]);
        steps[0].Missing.ShouldBeEmpty();
        steps[2].Missing.ShouldBeEmpty();
    }

    [Fact]
    public void A_missing_table_counts_as_zero()
    {
        var steps = SetupStatusCalculator.Compute([Step("a", [], ["A1"])], Counts(), None, None);
        steps[0].Status.ShouldBe(SetupStepStatuses.Red);
        steps[0].Tables.ShouldHaveSingleItem().Rows.ShouldBe(0);
    }

    [Fact]
    public void A_required_step_that_is_only_amber_keeps_the_next_one_grey()
    {
        SeedStep[] catalog = [Step("a", [], ["A1", "A2"]), Step("b", ["a"], ["B1"])];
        var steps = SetupStatusCalculator.Compute(catalog, Counts(("A1", 1)), None, None);
        steps[0].Status.ShouldBe(SetupStepStatuses.Amber);
        steps[1].Status.ShouldBe(SetupStepStatuses.Grey);
        steps[1].Missing.ShouldBe(["a"]);
    }

    [Fact]
    public void A_filled_step_behind_a_missing_requirement_is_not_grey()
    {
        SeedStep[] catalog = [Step("a", [], ["A1"]), Step("b", ["a"], ["B1"])];
        var steps = SetupStatusCalculator.Compute(catalog, Counts(("B1", 4)), None, None);
        steps[1].Status.ShouldBe(SetupStepStatuses.Green);
        steps[1].Missing.ShouldBeEmpty();
    }

    [Fact]
    public void A_failed_job_that_wrote_nothing_leaves_the_step_red_with_the_failed_flag()
    {
        var steps = SetupStatusCalculator.Compute([Step("a", [], ["A1"])], Counts(), new HashSet<string> { "a" }, None);
        steps[0].Status.ShouldBe(SetupStepStatuses.Red);
        steps[0].Failed.ShouldBe(true);
    }

    [Fact]
    public void A_failed_job_that_wrote_some_rows_makes_the_step_amber()
    {
        var steps = SetupStatusCalculator.Compute([Step("a", [], ["A1", "A2"])], Counts(("A1", 3)), new HashSet<string> { "a" }, None);
        steps[0].Status.ShouldBe(SetupStepStatuses.Amber);
        steps[0].Failed.ShouldBe(true);
    }

    [Fact]
    public void A_completed_step_with_a_note_is_green_at_zero_rows()
    {
        var catalog = new[] { Step("a", [], ["A1"], SetupNotes.NothingToImport) };
        var steps = SetupStatusCalculator.Compute(catalog, Counts(), None, new HashSet<string> { "a" });
        steps[0].Status.ShouldBe(SetupStepStatuses.Green);
        steps[0].Note.ShouldBe(SetupNotes.NothingToImport);

        SetupStatusCalculator.Compute(catalog, Counts(), None, None)[0].Status.ShouldBe(SetupStepStatuses.Red);
    }

    [Fact]
    public void An_unpopulated_view_counts_as_empty_and_keeps_its_flag()
    {
        var counts = new Dictionary<string, TableCount>
        {
            ["V1"] = new("V1", 0, false),
            ["V2"] = new("V2", 7, true),
        };
        var steps = SetupStatusCalculator.Compute([Step("v", [], ["V1", "V2"])], counts, None, None);
        steps[0].Status.ShouldBe(SetupStepStatuses.Amber);
        steps[0].Tables.Select(t => t.Populated).ShouldBe([false, true]);
    }

    [Fact]
    public void The_real_catalog_on_an_empty_database_is_red_except_the_chain_behind_it()
    {
        var books = SeedCatalog.Find("books")!.Tables.ToDictionary(t => t.Name, t => new TableCount(t.Name, 10));
        var steps = SetupStatusCalculator.Compute(SeedCatalog.Steps, books, None, None);

        steps.Single(s => s.Key == "books").Status.ShouldBe(SetupStepStatuses.Green);
        steps.Single(s => s.Key == "elb1871-text").Status.ShouldBe(SetupStepStatuses.Red);
        var verses = steps.Single(s => s.Key == "elb1871-verses");
        verses.Status.ShouldBe(SetupStepStatuses.Grey);
        verses.Missing.ShouldBe(["elb1871-text"]);
        steps.Single(s => s.Key == "step-text").Wired.ShouldBeFalse();
    }
}
