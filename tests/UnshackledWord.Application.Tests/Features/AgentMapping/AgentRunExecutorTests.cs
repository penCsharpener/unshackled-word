using UnshackledWord.Application.Features.AgentMapping;
using UnshackledWord.Domain.WebApi.Maintainer;

namespace UnshackledWord.Application.Tests.Features.AgentMapping;

public class AgentRunExecutorTests
{
    [Fact]
    public async Task AllPackagesAreSent_ProposalsStored_AndUsageSummedPerPackageAndRun()
    {
        var h = new AgentRunHarness(verses: 6, versesPerPackage: 2);
        var runId = h.NewRun();

        await h.Executor().RunAsync(runId);

        var run = h.Runs.Get(runId);
        run.Status.ShouldBe(AgentRunStatuses.Completed);
        h.Client.Calls.ShouldBe(3);
        h.Proposals.Proposed.Distinct().Count().ShouldBe(6);
        run.InputTokens.ShouldBe(900);
        run.CachedInputTokens.ShouldBe(300);
        run.OutputTokens.ShouldBe(150);
        h.Runs.Usage.Select(u => u.PackageId).Distinct().Count().ShouldBe(3, "tokens are stored per package");
        h.Client.Requests.ShouldAllBe(r => r.StaticPrefix.Length == 400, "the static prefix is the same for every package");
        h.Builder.Requests[0].ExistingRunId.ShouldBe(runId, "packages belong to the run, no new run is created");
    }

    [Fact]
    public async Task RejectedVerse_IsResubmittedOnce_WithTheValidatorMessagesAppended()
    {
        var h = new AgentRunHarness(verses: 2, versesPerPackage: 2);
        h.Proposals.Rejects = (refId, attempt) => refId == AgentRunHarness.RefOf(2) && attempt == 1;
        var runId = h.NewRun();

        await h.Executor().RunAsync(runId);

        h.Client.Calls.ShouldBe(2);
        var retry = h.Client.Requests.Last();
        retry.Turns.Count.ShouldBe(2);
        retry.Turns[0].Role.ShouldBe(AgentModelTurn.Assistant);
        retry.Turns[1].Role.ShouldBe(AgentModelTurn.User);
        retry.Turns[1].Text.ShouldContain("v2 g1: source s9 belongs to another verse");
        h.Proposals.Proposed.Distinct().Count().ShouldBe(2);
        h.Runs.Get(runId).Status.ShouldBe(AgentRunStatuses.Completed);
    }

    [Fact]
    public async Task ResubmissionStopsAfterTwoRetries()
    {
        var h = new AgentRunHarness(verses: 1, versesPerPackage: 1);
        h.Proposals.Rejects = (_, _) => true;
        var runId = h.NewRun();

        await h.Executor().RunAsync(runId);

        h.Client.Calls.ShouldBe(1 + AgentRunExecutor.MaxResubmissions);
        h.Proposals.Proposed.ShouldBeEmpty();
        h.Runs.Get(runId).StatusNote.ShouldContain("problem(s) left");
    }

    [Fact]
    public async Task OnlyTheRejectedVersesAreAskedAgain()
    {
        var h = new AgentRunHarness(verses: 2, versesPerPackage: 2);
        h.Proposals.Rejects = (refId, attempt) => refId == AgentRunHarness.RefOf(1) && attempt == 1;
        h.Client.Script = (request, number) => Task.FromResult(number == 1 ? h.Client.Answer(request) : h.Client.Answer(request, ["v1"]));
        var runId = h.NewRun();

        await h.Executor().RunAsync(runId);

        h.Proposals.Attempts[AgentRunHarness.RefOf(2)].ShouldBe(1, "the accepted verse is not sent a second time");
        h.Proposals.Attempts[AgentRunHarness.RefOf(1)].ShouldBe(2);
    }

    [Fact]
    public async Task BudgetCap_StopsBeforeThePackageThatWouldPassIt_AndResumeContinuesWithoutRedoingStoredVerses()
    {
        var h = new AgentRunHarness(verses: 6, versesPerPackage: 2);
        var runId = h.NewRun(maxTokens: 500); // prefix 100 + package 200 + answer 60 = 360 per package; 350 are spent after the first call

        await h.Executor().RunAsync(runId);

        var run = h.Runs.Get(runId);
        run.Status.ShouldBe(AgentRunStatuses.BudgetExceeded);
        h.Client.Calls.ShouldBe(1, "the second package was not sent");
        h.Proposals.Proposed.Distinct().Count().ShouldBe(2);
        run.StatusNote.ShouldContain("MaxTokens 500");

        var prepared = await h.Service().PrepareResumeAsync(new ResumeAgentRunRequest { RunId = runId, MaxTokens = 5000 }, activeInProcess: false);
        prepared.Outcome.ShouldBe(AgentRunPrepareOutcome.Ok);
        run.Status.ShouldBe(AgentRunStatuses.Queued);

        await h.Executor().RunAsync(runId);

        run.Status.ShouldBe(AgentRunStatuses.Completed);
        h.Client.Calls.ShouldBe(3);
        h.Proposals.Proposed.Distinct().Count().ShouldBe(6);
        h.Proposals.Attempts[AgentRunHarness.RefOf(1)].ShouldBe(1, "verses stored before the stop are not asked again");
        h.Builder.Requests.Last().SkipRefIds!.ShouldContain(AgentRunHarness.RefOf(1));
    }

    [Fact]
    public async Task CostCap_StopsTheRunToo()
    {
        var h = new AgentRunHarness(verses: 4, versesPerPackage: 2, profileSettings: "Models:m:Tag=m;Models:m:InputPricePerMillion=1000;Models:m:OutputPricePerMillion=1000");
        var runId = h.NewRun(maxCost: 0.5m); // 350 tokens * 1000 / 1M = 0.35 USD after the first call; the next package estimate is 0.36

        await h.Executor().RunAsync(runId);

        h.Runs.Get(runId).Status.ShouldBe(AgentRunStatuses.BudgetExceeded);
        h.Runs.Get(runId).CostUsd.ShouldBe(0.35m);
        h.Client.Calls.ShouldBe(1);
    }

    [Fact]
    public async Task Cancel_StopsAfterThePackageInFlight_AndKeepsTheCancelledStatus()
    {
        var h = new AgentRunHarness(verses: 6, versesPerPackage: 2);
        var runId = h.NewRun();
        h.Client.Script = async (request, number) =>
        {
            if (number == 1)
            {
                await h.Runs.SetRunStatusAsync(runId, AgentRunStatuses.Cancelled, "Cancelled by the maintainer"); // the maintainer cancels during the first call
            }

            return h.Client.Answer(request);
        };

        await h.Executor().RunAsync(runId);

        h.Client.Calls.ShouldBe(1);
        h.Proposals.Proposed.Distinct().Count().ShouldBe(2, "the package in flight finished and was stored");
        h.Runs.Get(runId).Status.ShouldBe(AgentRunStatuses.Cancelled);
    }

    [Fact]
    public async Task SchemaFailure_ShrinksThePackageAndTheRestOfTheRun()
    {
        var h = new AgentRunHarness(verses: 8, versesPerPackage: 4);
        h.Client.Script = (request, _) =>
        {
            var count = System.Text.Json.JsonDocument.Parse(request.PackageJson).RootElement.GetProperty("verses").GetArrayLength();
            if (count > 2)
            {
                throw new AgentModelException(AgentModelErrorKind.SchemaFailure, "cut off", outputTokens: 1000, inputTokens: 200);
            }

            return Task.FromResult(h.Client.Answer(request));
        };
        var runId = h.NewRun();

        await h.Executor().RunAsync(runId);

        var run = h.Runs.Get(runId);
        run.Status.ShouldBe(AgentRunStatuses.Completed);
        h.Proposals.Proposed.Distinct().Count().ShouldBe(8);
        h.Client.Requests.Count(r => System.Text.Json.JsonDocument.Parse(r.PackageJson).RootElement.GetProperty("verses").GetArrayLength() > 2).ShouldBe(1,
            "one failure, then every package (also the ones built before) is cut to at most 2 verses");
        h.Builder.Requests.Any(r => r.MaxVersesPerPackage == 2 && r.ExistingRunId == runId).ShouldBeTrue();
        run.OutputTokens.ShouldBeGreaterThanOrEqualTo(1000, "the cut-off answer is billed and stored");
    }

    [Fact]
    public async Task SchemaFailureOnASingleVerse_FailsThatPackageOnly()
    {
        var h = new AgentRunHarness(verses: 2, versesPerPackage: 1);
        h.Client.Script = (request, number) => number == 1
            ? throw new AgentModelException(AgentModelErrorKind.SchemaFailure, "not json")
            : Task.FromResult(h.Client.Answer(request));
        var runId = h.NewRun();

        await h.Executor().RunAsync(runId);

        h.Runs.Get(runId).Status.ShouldBe(AgentRunStatuses.Completed);
        h.Runs.Get(runId).StatusNote.ShouldContain("1 package(s) failed");
        h.Proposals.Proposed.Distinct().Count().ShouldBe(1);
        await h.Store.Received(1).SetPackageStatusAsync(Arg.Any<Guid>(), AgentPackageStatuses.Failed, false, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AuthError_FailsTheRunAtOnce()
    {
        var h = new AgentRunHarness(verses: 4, versesPerPackage: 2);
        h.Client.Script = (_, _) => throw new AgentModelException(AgentModelErrorKind.Auth, "HTTP 401: bad key");
        var runId = h.NewRun();

        await h.Executor().RunAsync(runId);

        h.Client.Calls.ShouldBe(1);
        h.Runs.Get(runId).Status.ShouldBe(AgentRunStatuses.Failed);
        h.Runs.Get(runId).StatusNote.ShouldContain("Auth");
    }

    [Fact]
    public async Task ThreeTransientFailuresInARow_FailTheRun()
    {
        var h = new AgentRunHarness(verses: 10, versesPerPackage: 1);
        h.Client.Script = (_, _) => throw new AgentModelException(AgentModelErrorKind.Transient, "HTTP 503");
        var runId = h.NewRun();

        await h.Executor().RunAsync(runId);

        h.Client.Calls.ShouldBe(AgentRunExecutor.MaxConsecutiveTransientFailures);
        h.Runs.Get(runId).Status.ShouldBe(AgentRunStatuses.Failed);
    }

    [Fact]
    public async Task FirstPackageRunsAlone_ThenTheProfileParallelismApplies()
    {
        var h = new AgentRunHarness(verses: 8, versesPerPackage: 1, profileSettings: "MaxParallel=3");
        h.Client.Script = async (request, number) =>
        {
            if (number == 1)
            {
                h.Client.MaxConcurrent.ShouldBe(1, "cache warm-up: nothing else is in flight during the first call");
            }

            await Task.Delay(40);

            return h.Client.Answer(request);
        };
        var runId = h.NewRun();

        await h.Executor().RunAsync(runId);

        h.Runs.Get(runId).Status.ShouldBe(AgentRunStatuses.Completed);
        h.Client.MaxConcurrent.ShouldBe(3);
        h.Proposals.Proposed.Distinct().Count().ShouldBe(8);
    }

    [Fact]
    public async Task ProfileWithoutKey_FailsTheRunWithoutCallingTheModel()
    {
        var h = new AgentRunHarness(verses: 2, profileSettings: "BaseUrl=https://api.example.com/v1");
        var runId = h.NewRun();

        await h.Executor().RunAsync(runId);

        h.Client.Calls.ShouldBe(0);
        h.Runs.Get(runId).Status.ShouldBe(AgentRunStatuses.Failed);
        h.Runs.Get(runId).StatusNote.ShouldContain("no API key");
    }

    [Fact]
    public async Task NothingLeftToDo_CompletesWithoutACall()
    {
        var h = new AgentRunHarness(verses: 2, versesPerPackage: 2);
        h.Runs.Proposed.Add(AgentRunHarness.RefOf(1));
        h.Runs.Proposed.Add(AgentRunHarness.RefOf(2));
        var runId = h.NewRun();

        await h.Executor().RunAsync(runId);

        h.Client.Calls.ShouldBe(0);
        h.Runs.Get(runId).Status.ShouldBe(AgentRunStatuses.Completed);
    }
}
