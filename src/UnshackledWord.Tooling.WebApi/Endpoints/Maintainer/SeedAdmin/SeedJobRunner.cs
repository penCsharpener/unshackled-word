using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.ExportSeed;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.SeedAdmin;

/// <summary>
/// Runs one seed job at a time in the background of this API process and keeps the state of the running or last job for polling
/// (<c>GET maintainer/seed/status</c>). Shares its exclusivity with the zip download (<see cref="SeedExportCoordinator"/>), so an export
/// and an import never run side by side. The state lives in memory only: a restart forgets it, the databases and files do not.
/// </summary>
public sealed class SeedJobRunner
{
    /// <summary>Log lines kept per job; the oldest are dropped.</summary>
    public const int MaxLogLines = 500;

    /// <summary>The work of a job: reports progress lines and returns the result shown on the page.</summary>
    public delegate Task<SeedJobResult> Work(IProgress<string> progress, CancellationToken token);

    private readonly SeedExportCoordinator _coordinator;
    private readonly TimeProvider _time;
    private readonly ILogger<SeedJobRunner> _logger;
    private readonly object _gate = new();
    private SeedJobResponse? _job;

    public SeedJobRunner(SeedExportCoordinator coordinator, TimeProvider time, ILogger<SeedJobRunner> logger)
    {
        _coordinator = coordinator;
        _time = time;
        _logger = logger;
    }

    /// <summary>Starts the job and returns at once; null when another seed job or export is running.</summary>
    public SeedJobResponse? TryStart(string operation, string kind, Work work)
    {
        if (!_coordinator.TryEnter())
        {
            return null;
        }

        var job = new SeedJobResponse
        {
            Id = Guid.NewGuid().ToString("N"),
            Operation = operation,
            Kind = kind,
            Status = SeedJobResponse.Running,
            StartedAt = _time.GetUtcNow()
        };

        lock (_gate)
        {
            _job = job;
        }

        _ = Task.Run(() => RunAsync(job, work));

        return Snapshot();
    }

    /// <summary>A copy of the running or last job, or null when none ran yet.</summary>
    public SeedJobResponse? Snapshot()
    {
        lock (_gate)
        {
            return _job is null ? null : Copy(_job);
        }
    }

    private async Task RunAsync(SeedJobResponse job, Work work)
    {
        var progress = new Progress(this, job);

        try
        {
            progress.Report($"Started {job.Operation} ({job.Kind})");
            var result = await work(progress, CancellationToken.None);

            lock (_gate)
            {
                job.Result = result;
                job.Status = SeedJobResponse.Succeeded;
            }

            progress.Report("Finished");
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Seed job {Operation} {Kind} failed", job.Operation, job.Kind);

            lock (_gate)
            {
                job.Error = e.Message;
                job.Status = SeedJobResponse.Failed;
            }

            progress.Report($"Failed: {e.Message}");
        }
        finally
        {
            lock (_gate)
            {
                job.FinishedAt = _time.GetUtcNow();
            }

            _coordinator.Exit();
        }
    }

    private void Append(SeedJobResponse job, string line)
    {
        lock (_gate)
        {
            job.Log.Add($"{_time.GetUtcNow():HH:mm:ss} {line}");

            if (job.Log.Count > MaxLogLines)
            {
                job.Log.RemoveRange(0, job.Log.Count - MaxLogLines);
            }
        }
    }

    private static SeedJobResponse Copy(SeedJobResponse job) => new()
    {
        Id = job.Id,
        Operation = job.Operation,
        Kind = job.Kind,
        Status = job.Status,
        StartedAt = job.StartedAt,
        FinishedAt = job.FinishedAt,
        Log = [.. job.Log],
        Error = job.Error,
        Result = job.Result
    };

    private sealed class Progress : IProgress<string>
    {
        private readonly SeedJobRunner _runner;
        private readonly SeedJobResponse _job;

        public Progress(SeedJobRunner runner, SeedJobResponse job)
        {
            _runner = runner;
            _job = job;
        }

        public void Report(string value) => _runner.Append(_job, value);
    }
}
