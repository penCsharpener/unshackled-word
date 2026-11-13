using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using UnshackledWord.Tooling.WebApi.Endpoints.DataQuality;

namespace UnshackledWord.Tooling.WebApi.Tests;

public class StatsRefreshStatementsTests
{
    private static readonly string[] Order = ["StatsStrongsRenderings", "StatsStrongs", "StatsGermanLemma", "StatsGermanForms"];

    [Fact]
    public void FirstRefresh_IsPlain_ForEveryView_InPipelineOrder()
    {
        var sql = DqSql.StatsRefreshStatements(new HashSet<string>());

        sql.Count.ShouldBe(4);
        sql.ShouldAllBe(s => s.StartsWith("REFRESH MATERIALIZED VIEW \"unshackled-word\"."));
        sql.Select((s, i) => s.EndsWith($"\"{Order[i]}\"")).ShouldAllBe(x => x);
    }

    [Fact]
    public void PopulatedViews_AreRefreshedConcurrently()
    {
        var sql = DqSql.StatsRefreshStatements(Order.ToHashSet());

        sql.ShouldAllBe(s => s.StartsWith("REFRESH MATERIALIZED VIEW CONCURRENTLY "));
        sql.Select((s, i) => s.EndsWith($"\"{Order[i]}\"")).ShouldAllBe(x => x);
    }

    [Fact]
    public void Mixed_PicksPerView()
    {
        var sql = DqSql.StatsRefreshStatements(new HashSet<string> { "StatsStrongs" });

        sql[0].ShouldNotContain("CONCURRENTLY");
        sql[1].ShouldContain("CONCURRENTLY");
        sql[2].ShouldNotContain("CONCURRENTLY");
    }
}

public class DqOverviewServiceRefreshTests
{
    private readonly IDqFactsRepository _facts = Substitute.For<IDqFactsRepository>();
    private readonly IDqCheckRepository _checks = Substitute.For<IDqCheckRepository>();
    private readonly DqRefreshCoordinator _coordinator;
    private readonly DqOverviewService _sut;

    public DqOverviewServiceRefreshTests()
    {
        _checks.CountAsync(Arg.Any<string>(), Arg.Any<DqFacets>(), Arg.Any<CancellationToken>()).Returns(new CheckCount(1, 0, null));
        _facts.GetTotalsAsync(Arg.Any<DqFacets>(), Arg.Any<CancellationToken>()).Returns(new UnshackledWord.Domain.WebApi.DataQuality.OverviewTotals());
        _coordinator = new DqRefreshCoordinator(Substitute.For<IServiceScopeFactory>(), NullLogger<DqRefreshCoordinator>.Instance);
        _sut = new DqOverviewService(_facts, _checks, _coordinator, new MemoryCache(new MemoryCacheOptions()));
    }

    [Fact]
    public async Task Overview_CarriesTheSeedImports_EvenBeforeTheFirstSnapshot()
    {
        _facts.GetStateAsync(Arg.Any<CancellationToken>()).Returns(new DqState());
        _facts.GetSeedImportsAsync(Arg.Any<CancellationToken>()).Returns(
            [new UnshackledWord.Domain.WebApi.DataQuality.SeedImportInfo { Kind = "morphology", Orphaned = 12, ReportPath = "temp/seed-report/morphology-abc.csv" }]);

        var response = await _sut.GetAsync(new DqFacets(null, null), CancellationToken.None);

        response.SeedImports.ShouldHaveSingleItem().Orphaned.ShouldBe(12);
    }

    [Fact]
    public async Task Refresh_RunsFactsThenCountsThenStats_InThatOrder()
    {
        await _sut.RefreshAsync(CancellationToken.None);

        Received.InOrder(() =>
        {
            _facts.RefreshViewAsync(Arg.Any<CancellationToken>());
            _facts.SaveCountsAsync(Arg.Any<IReadOnlyDictionary<string, int>>(), Arg.Any<CancellationToken>());
            _facts.RefreshStatsViewsAsync(Arg.Any<CancellationToken>());
            _facts.SaveStatsRefreshedAtAsync(Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task FailingStatsRefresh_KeepsTheFactsTimestamp_AndNeverStampsTheStats()
    {
        _facts.RefreshStatsViewsAsync(Arg.Any<CancellationToken>()).Returns(Task.FromException(new InvalidOperationException("boom")));

        await Should.ThrowAsync<InvalidOperationException>(() => _sut.RefreshAsync(CancellationToken.None));

        await _facts.Received(1).SaveCountsAsync(Arg.Any<IReadOnlyDictionary<string, int>>(), Arg.Any<CancellationToken>());
        await _facts.DidNotReceive().SaveStatsRefreshedAtAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FailingFactsRefresh_SkipsEverythingElse()
    {
        _facts.RefreshViewAsync(Arg.Any<CancellationToken>()).Returns(Task.FromException(new InvalidOperationException("boom")));

        await Should.ThrowAsync<InvalidOperationException>(() => _sut.RefreshAsync(CancellationToken.None));

        await _facts.DidNotReceive().RefreshStatsViewsAsync(Arg.Any<CancellationToken>());
        await _facts.DidNotReceive().SaveStatsRefreshedAtAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Coordinator_ReleasesTheFlagWhenTheStatsStepFails_AndRejectsASecondStartWhileRunning()
    {
        var gate = new TaskCompletionSource();
        _facts.RefreshStatsViewsAsync(Arg.Any<CancellationToken>()).Returns(async _ =>
        {
            await gate.Task;
            throw new InvalidOperationException("boom");
        });
        var services = new ServiceCollection();
        services.AddScoped(_ => _sut);
        var coordinator = new DqRefreshCoordinator(services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(), NullLogger<DqRefreshCoordinator>.Instance);

        coordinator.TryStart().ShouldBeTrue();
        coordinator.IsRunning.ShouldBeTrue();
        coordinator.TryStart().ShouldBeFalse();

        gate.SetResult();
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (coordinator.IsRunning && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20);
        }

        coordinator.IsRunning.ShouldBeFalse();
        coordinator.TryStart().ShouldBeTrue();
    }
}
