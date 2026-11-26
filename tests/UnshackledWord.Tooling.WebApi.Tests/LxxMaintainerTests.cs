using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using UnshackledWord.Application.Features.Lxx;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.ExportSeed;
using UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.SeedAdmin;
using StartEndpoint = UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.Lxx.StartPrefill.Endpoint;
using StatusEndpoint = UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.Lxx.GetStatus.Endpoint;

namespace UnshackledWord.Tooling.WebApi.Tests;

/// <summary>Roadmap 0028.6: maintainer endpoints of LXX pass 1 (202 with job, 409 when busy, status counts).</summary>
public class LxxMaintainerTests
{
    private readonly ILxxNtFormRepository _repository = Substitute.For<ILxxNtFormRepository>();
    private readonly SeedJobRunner _runner = new(new SeedExportCoordinator(), TimeProvider.System, NullLogger<SeedJobRunner>.Instance);

    private StartEndpoint StartEndpointFor()
    {
        _repository.GetNtAnalysesAsync(Arg.Any<CancellationToken>()).Returns([new NtFormAnalysis("λόγου", "λόγος", "G3056", "N-GSM")]);
        _repository.GetLxxWordsAsync(Arg.Any<CancellationToken>()).Returns([new LxxPrefillWord(1, 1, "", "λόγου")]);
        _repository.GetTagsAsync(Arg.Any<CancellationToken>()).Returns([]);
        _repository.ApplyAsync(Arg.Any<IReadOnlyList<LxxNtFormProposal>>(), Arg.Any<CancellationToken>()).Returns(new LxxNtFormApplyResult(1, 0, 0));
        var tagger = new LxxNtFormTagger(_repository, NullLogger<LxxNtFormTagger>.Instance);
        var scopes = new ServiceCollection().AddSingleton(tagger).BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

        return Factory.Create<StartEndpoint>(_runner, scopes);
    }

    [Fact]
    public async Task Start_Answers202AndRunsTheTaggerWithCoverageInTheLog()
    {
        var endpoint = StartEndpointFor();

        await endpoint.HandleAsync(CancellationToken.None);

        endpoint.HttpContext.Response.StatusCode.ShouldBe(StatusCodes.Status202Accepted);
        endpoint.Response.Job.Operation.ShouldBe(StartLxxPrefillResponse.Operation);

        for (var i = 0; i < 300 && _runner.Snapshot()?.Status == SeedJobResponse.Running; i++)
        {
            await Task.Delay(10);
        }

        var job = _runner.Snapshot().ShouldNotBeNull();
        job.Status.ShouldBe(SeedJobResponse.Succeeded);
        job.Log.ShouldContain(line => line.Contains(" Total: 1 / 1 words tagged"));
        await _repository.Received(1).ApplyAsync(Arg.Is<IReadOnlyList<LxxNtFormProposal>>(p => p.Count == 1), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Start_WhileAJobRuns_Answers409AndWritesNothing()
    {
        var gate = new TaskCompletionSource();
        _runner.TryStart("other", "all", async (_, _) => { await gate.Task; return new SeedJobResult(); }).ShouldNotBeNull();
        var endpoint = StartEndpointFor();

        await endpoint.HandleAsync(CancellationToken.None);

        endpoint.HttpContext.Response.StatusCode.ShouldBe(StatusCodes.Status409Conflict);
        await _repository.DidNotReceive().ApplyAsync(Arg.Any<IReadOnlyList<LxxNtFormProposal>>(), Arg.Any<CancellationToken>());
        gate.SetResult();
    }

    [Fact]
    public async Task Status_ReturnsCountsAndOnlyAPrefillJob()
    {
        _repository.GetStatusCountsAsync(Arg.Any<CancellationToken>()).Returns(
            new LxxStatusCounts(23617, 486313, [new LxxTagCount("proposed", "nt-form", 265807), new LxxTagCount("confirmed", "human", 3)]));
        _runner.TryStart("export", "all", (_, _) => Task.FromResult(new SeedJobResult())).ShouldNotBeNull();
        var endpoint = Factory.Create<StatusEndpoint>(_runner, _repository);

        await endpoint.HandleAsync(CancellationToken.None);

        var response = endpoint.Response;
        response.Verses.ShouldBe(23617);
        response.Words.ShouldBe(486313);
        response.Tags.Select(t => (t.Status, t.Source, t.Count)).ShouldBe([("proposed", "nt-form", 265807), ("confirmed", "human", 3)]);
        response.Job.ShouldBeNull();
        response.Running.ShouldBeFalse();
    }

    [Fact]
    public void Endpoints_RequireLogin()
    {
        foreach (var type in new[] { typeof(StartEndpoint), typeof(StatusEndpoint) })
        {
            type.GetCustomAttributes(inherit: true).Select(a => a.GetType().Name).ShouldContain("RequiresLoginAttribute");
        }
    }
}
