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
    public void Only_the_derived_group_is_not_wired()
    {
        SeedCatalog.Steps.Where(s => s.Wired).Select(s => s.Group).Distinct().ShouldBe([SetupGroups.Base, SetupGroups.Step, SetupGroups.Other], ignoreOrder: true);
        SeedCatalog.Steps.Where(s => s.Group is SetupGroups.Base or SetupGroups.Step or SetupGroups.Other).ShouldAllBe(s => s.Wired);
        SeedCatalog.Steps.Where(s => s.Group == SetupGroups.Derived).ShouldAllBe(s => !s.Wired);
    }

    [Fact]
    public void Step_steps_are_downloads_from_github_with_a_library_runner()
    {
        var steps = SeedCatalog.Steps.Where(s => s.Group == SetupGroups.Step).ToList();
        steps.Count.ShouldBe(6);
        foreach (var step in steps)
        {
            step.Source.ShouldBe(SetupSources.Download, step.Key);
            step.SourceHost.ShouldBe("github.com", step.Key);
            step.Runner.ShouldNotBeNull(step.Key);
            step.Runner!.Namespace!.StartsWith("UnshackledWord.Seeding.StepBible").ShouldBeTrue(step.Key);
        }
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
        SeedCatalog.Steps.Where(s => s.Runner is not null).Count().ShouldBe(19);
    }

    [Fact]
    public void Other_steps_have_a_library_runner_and_only_sbl_is_resumable()
    {
        var steps = SeedCatalog.Steps.Where(s => s.Group == SetupGroups.Other).ToList();
        steps.Count.ShouldBe(9);
        foreach (var step in steps)
        {
            step.Runner.ShouldNotBeNull(step.Key);
            step.Runner!.Namespace!.StartsWith("UnshackledWord.Seeding.").ShouldBeTrue(step.Key);
        }

        steps.Where(s => s.Resumable).Select(s => s.Key).ShouldBe(["sbl", "lxx-tags", "revision-seed"], ignoreOrder: true);
        steps.Where(s => s.Note is not null).Select(s => s.Key).ShouldBe(["tsk-curation", "lxx-tags", "revision-seed"], ignoreOrder: true);
    }
}
