using UnshackledWord.Seeding.Derived;

namespace UnshackledWord.Tooling.SeedDb.Tests.Seeding;

public class StatsViewsRunnerTests
{
    [Fact]
    public void Views_are_refreshed_in_dependency_order_with_the_facts_first()
    {
        StatsViewsRunner.Views.ShouldBe(["DqWordFacts", "StatsStrongsRenderings", "StatsStrongs", "StatsGermanLemma", "StatsGermanForms"]);
    }

    [Fact]
    public void An_unpopulated_view_is_refreshed_plain_and_a_populated_one_concurrently()
    {
        var statements = StatsViewsRunner.RefreshStatements(new HashSet<string> { "StatsStrongs" });

        statements.Count.ShouldBe(5);
        statements[0].ShouldBe("REFRESH MATERIALIZED VIEW \"unshackled-word\".\"DqWordFacts\"");
        statements[2].ShouldBe("REFRESH MATERIALIZED VIEW CONCURRENTLY \"unshackled-word\".\"StatsStrongs\"");
    }
}
