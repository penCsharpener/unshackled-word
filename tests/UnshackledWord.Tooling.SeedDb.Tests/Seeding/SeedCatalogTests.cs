using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UnshackledWord.Domain.WebApi.Admin;
using UnshackledWord.Seeding.Catalog;
using UnshackledWord.Tooling.SeedDb.Extensions;
using UnshackledWord.Tooling.SeedDb.Services;

namespace UnshackledWord.Tooling.SeedDb.Tests.Seeding;

public class SeedCatalogTests
{
    [Fact]
    public void Has_22_steps_with_unique_keys()
    {
        SeedCatalog.Steps.Count.ShouldBe(22);
        SeedCatalog.Steps.Select(s => s.Key).Distinct().Count().ShouldBe(22);
        SeedCatalog.Steps.ShouldAllBe(s => SetupGroups.All.Contains(s.Group));
    }

    [Fact]
    public void Only_the_base_group_is_wired()
    {
        SeedCatalog.Steps.Where(s => s.Wired).Select(s => s.Group).Distinct().ShouldBe([SetupGroups.Base]);
        SeedCatalog.Steps.Where(s => s.Group == SetupGroups.Base).ShouldAllBe(s => s.Wired);
        SeedCatalog.Find("step-text")!.Wired.ShouldBeFalse();
    }

    [Fact]
    public void Every_requires_key_exists_and_comes_earlier()
    {
        var seen = new HashSet<string>();
        foreach (var step in SeedCatalog.Steps)
        {
            foreach (var required in step.Requires)
            {
                seen.ShouldContain(required, $"{step.Key} requires {required}, which must exist earlier in the catalog");
            }

            seen.Add(step.Key);
        }
    }

    [Fact]
    public void Every_runner_type_is_registered_in_DI()
    {
        var services = new ServiceCollection();
        services.AddSeedDbServices(new ConfigurationBuilder().Build());

        foreach (var step in SeedCatalog.Steps)
        {
            var runner = SeedRunnerMap.RunnerOf(step);
            if (runner is null)
            {
                // read-only rows and refresh-only steps
                new[] { "books", "stats-views" }.ShouldContain(step.Key);
                continue;
            }

            services.Any(d => d.ServiceType == runner).ShouldBeTrue($"{runner.Name} of step {step.Key} is not registered");
        }
    }

    [Fact]
    public void Wired_steps_have_a_runner_in_the_library_unless_read_only()
    {
        foreach (var step in SeedCatalog.Steps.Where(s => s.Wired && s.Runnable))
        {
            step.Runner.ShouldNotBeNull(step.Key);
        }
    }

    [Fact]
    public void The_cli_runs_the_runners_in_catalog_order()
    {
        var expected = SeedCatalog.Steps.Select(SeedRunnerMap.RunnerOf).OfType<Type>().ToArray();
        SeedRunnerMap.RunnerTypes().ShouldBe(expected);
        SeedRunnerMap.RunnerTypes().Length.ShouldBe(20);
    }
}
