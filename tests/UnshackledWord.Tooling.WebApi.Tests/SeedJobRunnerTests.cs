using Microsoft.Extensions.Logging.Abstractions;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.ExportSeed;
using UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.SeedAdmin;

namespace UnshackledWord.Tooling.WebApi.Tests;

public class SeedJobRunnerTests
{
    private static SeedJobRunner Create(SeedExportCoordinator? coordinator = null) =>
        new(coordinator ?? new SeedExportCoordinator(), TimeProvider.System, NullLogger<SeedJobRunner>.Instance);

    private static async Task<SeedJobResponse> WaitForAsync(SeedJobRunner runner, Func<SeedJobResponse, bool> done)
    {
        for (var i = 0; i < 200; i++)
        {
            var job = runner.Snapshot();

            if (job is not null && done(job))
            {
                return job;
            }

            await Task.Delay(10);
        }

        throw new TimeoutException("The job did not reach the expected state.");
    }

    [Fact]
    public void Snapshot_ShouldBeNull_WhenNoJobRanYet()
    {
        Create().Snapshot().ShouldBeNull();
    }

    [Fact]
    public async Task TryStart_ShouldRunWorkAndKeepResultAndLog_WhenWorkSucceeds()
    {
        var runner = Create();
        var result = new SeedJobResult { Steps = [SeedStepReport.Skip("revision", "import", SeedSkipReason.TablesNotEmpty)] };

        var started = runner.TryStart(SeedOperations.Import, "revision", (progress, _) =>
        {
            progress.Report("halfway");

            return Task.FromResult(result);
        });

        started.ShouldNotBeNull();
        var job = await WaitForAsync(runner, j => j.Status != SeedJobResponse.Running && j.FinishedAt is not null);

        job.Status.ShouldBe(SeedJobResponse.Succeeded);
        job.Operation.ShouldBe(SeedOperations.Import);
        job.Result.ShouldBeSameAs(result);
        job.Log.ShouldContain(l => l.EndsWith("halfway"));
        job.Error.ShouldBeNull();
    }

    [Fact]
    public async Task TryStart_ShouldRecordTheErrorAndFreeTheSlot_WhenWorkThrows()
    {
        var coordinator = new SeedExportCoordinator();
        var runner = Create(coordinator);

        runner.TryStart(SeedOperations.Export, "all", (_, _) => throw new InvalidOperationException("disk full")).ShouldNotBeNull();
        var job = await WaitForAsync(runner, j => j.FinishedAt is not null);

        job.Status.ShouldBe(SeedJobResponse.Failed);
        job.Error.ShouldBe("disk full");
        job.Result.ShouldBeNull();
        coordinator.IsRunning.ShouldBeFalse();
    }

    [Fact]
    public async Task TryStart_ShouldRefuseASecondJob_WhileOneRuns()
    {
        var runner = Create();
        var release = new TaskCompletionSource();

        runner.TryStart(SeedOperations.Rebase, "all", async (_, _) =>
        {
            await release.Task;

            return new SeedJobResult();
        }).ShouldNotBeNull();

        runner.TryStart(SeedOperations.Update, "revision", (_, _) => Task.FromResult(new SeedJobResult())).ShouldBeNull();
        runner.Snapshot()!.Operation.ShouldBe(SeedOperations.Rebase);

        release.SetResult();
        await WaitForAsync(runner, j => j.FinishedAt is not null);

        runner.TryStart(SeedOperations.Update, "revision", (_, _) => Task.FromResult(new SeedJobResult())).ShouldNotBeNull();
    }

    [Fact]
    public void TryStart_ShouldRefuse_WhenTheZipExportRuns()
    {
        var coordinator = new SeedExportCoordinator();
        coordinator.TryEnter().ShouldBeTrue();

        Create(coordinator).TryStart(SeedOperations.Import, "all", (_, _) => Task.FromResult(new SeedJobResult())).ShouldBeNull();
    }

    [Fact]
    public async Task Log_ShouldKeepOnlyTheLastLines_WhenWorkReportsMore()
    {
        var runner = Create();

        runner.TryStart(SeedOperations.Import, "all", (progress, _) =>
        {
            for (var i = 0; i < SeedJobRunner.MaxLogLines + 50; i++)
            {
                progress.Report($"line {i}");
            }

            return Task.FromResult(new SeedJobResult());
        }).ShouldNotBeNull();

        var job = await WaitForAsync(runner, j => j.FinishedAt is not null);

        job.Log.Count.ShouldBe(SeedJobRunner.MaxLogLines);
        job.Log[^1].ShouldEndWith("Finished");
    }
}

public class StartSeedJobValidatorTests
{
    private static bool IsValid(string operation, string kind = "revision", bool confirm = false) =>
        new UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.SeedAdmin.StartJob.Validator()
            .Validate(new StartSeedJobRequest { Operation = operation, Kind = kind, ConfirmBackup = confirm }).IsValid;

    [Theory]
    [InlineData("export")]
    [InlineData("import")]
    [InlineData("update")]
    [InlineData("roundTrip")]
    public void KnownOperations_AreValid_WithoutBackupConfirmation(string operation)
    {
        IsValid(operation).ShouldBeTrue();
    }

    [Fact]
    public void Rebase_ShouldRequireTheBackupConfirmation()
    {
        IsValid(SeedOperations.Rebase, "all", confirm: false).ShouldBeFalse();
        IsValid(SeedOperations.Rebase, "all", confirm: true).ShouldBeTrue();
    }

    [Theory]
    [InlineData("", "revision")]
    [InlineData("drop", "revision")]
    [InlineData("export", "anchors")]
    [InlineData("export", "")]
    public void UnknownOperationOrKind_IsRejected(string operation, string kind)
    {
        IsValid(operation, kind).ShouldBeFalse();
    }
}

public class SeedOperationServiceKindTests
{
    [Theory]
    [InlineData("morphology", UnshackledWord.Infrastructure.Seed.SeedExportKind.Morphology)]
    [InlineData("MAPPINGS", UnshackledWord.Infrastructure.Seed.SeedExportKind.Mappings)]
    [InlineData("revision", UnshackledWord.Infrastructure.Seed.SeedExportKind.Revision)]
    [InlineData("all", UnshackledWord.Infrastructure.Seed.SeedExportKind.All)]
    [InlineData(null, UnshackledWord.Infrastructure.Seed.SeedExportKind.All)]
    public void ParseKind_ShouldMapTheRequestKind(string? kind, UnshackledWord.Infrastructure.Seed.SeedExportKind expected)
    {
        SeedOperationService.ParseKind(kind).ShouldBe(expected);
    }
}
