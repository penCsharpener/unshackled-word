using UnshackledWord.Application.Features.AgentMapping;
using UnshackledWord.Domain.WebApi.Maintainer;

namespace UnshackledWord.Application.Tests.Features.AgentMapping;

public class AgentRunServiceTests
{
    private static StartAgentRunRequest Request(string profile = "p") => new() { Book = AgentRunHarness.Book, FromChapter = 1, FromVerse = 1, ToChapter = 1, ToVerse = 6, Profile = profile };

    [Fact]
    public async Task Estimate_CountsPackagesPrefixAndCaps_WithoutStoringAnything()
    {
        var h = new AgentRunHarness(verses: 6, versesPerPackage: 2, profileSettings: "MaxTokensPerRun=1000");

        var result = await h.Service().EstimateAsync(Request());

        var e = result.Estimate!;
        e.Packages.ShouldBe(3);
        e.Verses.ShouldBe(6);
        e.PrefixTokens.ShouldBe(100);
        e.PromptVersion.ShouldBe("greek-v1");
        e.EstimatedTotalTokens.ShouldBeGreaterThan(1000);
        e.ExceedsCap.ShouldBeTrue();
        e.MaxTokens.ShouldBe(1000);
        e.Warnings.ShouldContain(w => w.Contains("budget-exceeded"));
        h.Builder.Requests.Single().DryRun.ShouldBeTrue();
        h.Builder.Packages.ShouldBeEmpty();
        h.Client.Calls.ShouldBe(0, "an estimate never calls a model");
    }

    [Fact]
    public async Task Estimate_UnknownProfile_IsABadRequest()
    {
        var result = await new AgentRunHarness().Service().EstimateAsync(Request("nope"));

        result.Outcome.ShouldBe(AgentRunPrepareOutcome.BadRequest);
        result.Error.ShouldContain("nope");
    }

    [Fact]
    public async Task Estimate_EmptyRange_IsNotFound()
    {
        var h = new AgentRunHarness(verses: 3);

        var result = await h.Service().EstimateAsync(new AgentRunRangeRequest { Book = AgentRunHarness.Book, FromChapter = 2, FromVerse = 1, Profile = "p" });

        result.Outcome.ShouldBe(AgentRunPrepareOutcome.NotFound);
    }

    [Fact]
    public async Task Start_WithoutApiKey_IsRefused_AndCreatesNoRun()
    {
        var h = new AgentRunHarness(profileSettings: "BaseUrl=https://api.example.com/v1");

        var result = await h.Service().CreateRunAsync(Request(), userId: 3);

        result.Outcome.ShouldBe(AgentRunPrepareOutcome.BadRequest);
        result.Error.ShouldContain("no API key");
        await h.Store.DidNotReceive().CreateRunAsync(Arg.Any<AgentRunInsert>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Start_CreatesAQueuedRunForTheAiUserOfTheModel_WithCapsAndEstimate()
    {
        var h = new AgentRunHarness(verses: 6, versesPerPackage: 2, profileSettings: "MaxTokensPerRun=100000");
        AgentRunInsert? inserted = null;
        h.Store.CreateRunAsync(Arg.Do<AgentRunInsert>(r => inserted = r), Arg.Any<CancellationToken>()).Returns(11);

        var result = await h.Service().CreateRunAsync(new StartAgentRunRequest { Book = 41, FromChapter = 1, FromVerse = 1, ToVerse = 6, Profile = "p", MaxCostUsd = 3m }, userId: 3);

        result.Outcome.ShouldBe(AgentRunPrepareOutcome.Ok);
        result.RunId.ShouldBe(11);
        inserted!.Status.ShouldBe(AgentRunStatuses.Queued);
        inserted.CreatedByUserId.ShouldBe(3);
        inserted.AiUserId.ShouldBe(7);
        inserted.Profile.ShouldBe("p");
        inserted.Model.ShouldBe("m");
        inserted.PromptVersion.ShouldBe("greek-v1");
        inserted.MaxTokens.ShouldBe((long?)100000);
        inserted.MaxCostUsd.ShouldBe(3m);
        (inserted.EstimatedTokens ?? 0).ShouldBeGreaterThan(0);
        inserted.FromRefId.ShouldBe(AgentRunHarness.RefOf(1));
        inserted.ToRefId.ShouldBe(AgentRunHarness.RefOf(6));
    }

    [Fact]
    public async Task Resume_RequiresAResumableRun_NotActive_AndACapAboveWhatIsSpent()
    {
        var h = new AgentRunHarness();
        var runId = h.NewRun(maxTokens: 500);
        var run = h.Runs.Get(runId);
        run.InputTokens = 400;
        run.OutputTokens = 50;

        (await h.Service().PrepareResumeAsync(new ResumeAgentRunRequest { RunId = runId }, false)).Outcome.ShouldBe(AgentRunPrepareOutcome.Conflict, "a queued run is not resumable");

        run.Status = AgentRunStatuses.BudgetExceeded;
        (await h.Service().PrepareResumeAsync(new ResumeAgentRunRequest { RunId = runId }, activeInProcess: true)).Outcome.ShouldBe(AgentRunPrepareOutcome.Conflict);
        (await h.Service().PrepareResumeAsync(new ResumeAgentRunRequest { RunId = runId, MaxTokens = 450 }, false)).Outcome.ShouldBe(AgentRunPrepareOutcome.BadRequest);
        (await h.Service().PrepareResumeAsync(new ResumeAgentRunRequest { RunId = 99 }, false)).Outcome.ShouldBe(AgentRunPrepareOutcome.NotFound);

        var ok = await h.Service().PrepareResumeAsync(new ResumeAgentRunRequest { RunId = runId, MaxTokens = 9000 }, false);

        ok.Outcome.ShouldBe(AgentRunPrepareOutcome.Ok);
        run.MaxTokens.ShouldBe((long?)9000);
        run.Status.ShouldBe(AgentRunStatuses.Queued);
    }

    [Fact]
    public async Task Cancel_SetsActiveRunsCancelled_AndRefusesFinishedOnes()
    {
        var h = new AgentRunHarness();
        var runId = h.NewRun();
        h.Runs.Get(runId).Status = AgentRunStatuses.Running;

        (await h.Service().CancelAsync(runId)).Outcome.ShouldBe(AgentRunPrepareOutcome.Ok);
        h.Runs.Get(runId).Status.ShouldBe(AgentRunStatuses.Cancelled);
        (await h.Service().CancelAsync(runId)).Outcome.ShouldBe(AgentRunPrepareOutcome.Conflict);
        (await h.Service().CancelAsync(42)).Outcome.ShouldBe(AgentRunPrepareOutcome.NotFound);
    }

    [Fact]
    public async Task ARunThatWasCancelledBeforeItStarted_NeverCallsTheModel()
    {
        var h = new AgentRunHarness();
        var runId = h.NewRun();
        await h.Service().CancelAsync(runId);

        await h.Executor().RunAsync(runId);

        h.Client.Calls.ShouldBe(0);
        h.Runs.Get(runId).Status.ShouldBe(AgentRunStatuses.Cancelled);
    }

    private static AgentRunService LxxService(AgentRunHarness h, out List<AgentBuildRequest> requests)
    {
        var seen = new List<AgentBuildRequest>();
        requests = seen;
        var builder = Substitute.For<IAgentPackageBuilder>();
        builder.BuildAsync(Arg.Do<AgentBuildRequest>(seen.Add), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            var package = new AgentPackageDto { PackageId = Guid.NewGuid().ToString(), Lang = "lxx" };
            package.Verses.Add(new AgentVerseDto { V = "v1", Ref = "Rt 1:1" });
            var response = new GetAgentPackageResponse { Stats = new AgentTokenStats { Verses = 1 } };
            response.Packages.Add(new AgentPackageEnvelope { Package = package, EstimatedTokens = 100, FromRefId = 8001001, ToRefId = 8001001 });
            return Task.FromResult<GetAgentPackageResponse?>(response);
        });

        return new AgentRunService(builder, h.Runs, h.Store, h.Prompts, h.Configuration);
    }

    private static StartAgentRunRequest Lxx(Action<StartAgentRunRequest>? configure = null)
    {
        var request = new StartAgentRunRequest { Book = 8, FromChapter = 1, FromVerse = 1, ToVerse = 4, Profile = "p", Mode = "lxx-tag" };
        configure?.Invoke(request);

        return request;
    }

    [Fact]
    public async Task LxxEstimateAndStart_CarryIncludeConfirmedAndTextForm_ToTheBuilderAndTheStoredRun()
    {
        var h = new AgentRunHarness();
        var service = LxxService(h, out var requests);
        AgentRunInsert? inserted = null;
        h.Store.CreateRunAsync(Arg.Do<AgentRunInsert>(r => inserted = r), Arg.Any<CancellationToken>()).Returns(5);

        var estimate = await service.EstimateAsync(Lxx(r => { r.IncludeConfirmed = true; r.TextForm = " TH "; }));
        var created = await service.CreateRunAsync(Lxx(r => { r.IncludeConfirmed = true; r.TextForm = "og"; }), userId: 3);

        estimate.Outcome.ShouldBe(AgentRunPrepareOutcome.Ok);
        created.Outcome.ShouldBe(AgentRunPrepareOutcome.Ok);
        requests.Select(r => (r.IncludeConfirmed, r.TextForm)).ShouldBe([(true, "th"), (true, "og")]);
        inserted!.IncludeConfirmed.ShouldBeTrue();
        inserted.TextForm.ShouldBe("og");
    }

    [Fact]
    public async Task LxxStart_WithoutTheOptions_StoresDefaults()
    {
        var h = new AgentRunHarness();
        var service = LxxService(h, out var requests);
        AgentRunInsert? inserted = null;
        h.Store.CreateRunAsync(Arg.Do<AgentRunInsert>(r => inserted = r), Arg.Any<CancellationToken>()).Returns(5);

        (await service.CreateRunAsync(Lxx(), userId: 3)).Outcome.ShouldBe(AgentRunPrepareOutcome.Ok);

        requests.Single().IncludeConfirmed.ShouldBeFalse();
        requests.Single().TextForm.ShouldBeNull();
        inserted!.IncludeConfirmed.ShouldBeFalse();
        inserted.TextForm.ShouldBeNull();
    }

    [Theory]
    [InlineData("x", false, "lxx-tag")]
    [InlineData("og", false, "redo")]
    [InlineData(null, true, "fill-open")]
    public async Task TheLxxOptions_AreABadRequest_ForOtherValuesAndOtherModes(string? textForm, bool includeConfirmed, string mode)
    {
        var h = new AgentRunHarness();
        var service = LxxService(h, out var requests);

        var result = await service.EstimateAsync(Lxx(r => { r.TextForm = textForm; r.IncludeConfirmed = includeConfirmed; r.Mode = mode; if (mode != "lxx-tag") { r.Book = 41; } }));

        result.Outcome.ShouldBe(AgentRunPrepareOutcome.BadRequest);
        requests.ShouldBeEmpty();
    }
}
