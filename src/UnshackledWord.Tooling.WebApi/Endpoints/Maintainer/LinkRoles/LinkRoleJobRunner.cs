using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.ExportSeed;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.LinkRoles;

/// <summary>
/// Runs one link-roles backfill job (roadmap 0018.4) at a time in the background and keeps the running or last job for polling
/// (<c>GET maintainer/link-roles/status</c>), like <see cref="SeedAdmin.SeedJobRunner"/>. Takes the seed lock
/// (<see cref="SeedExportCoordinator"/>), so the backfill never runs beside a seed job or export and they wait for it. State in memory only.
/// </summary>
public sealed class LinkRoleJobRunner
{
    public const int MaxLogLines = 500;

    public delegate Task<LinkRoleJobResult> Work(IProgress<string> progress, CancellationToken token);

    private readonly SeedExportCoordinator _coordinator;
    private readonly TimeProvider _time;
    private readonly ILogger<LinkRoleJobRunner> _logger;
    private readonly object _gate = new();
    private LinkRoleJobResponse? _job;
    private int _running;

    public LinkRoleJobRunner(SeedExportCoordinator coordinator, TimeProvider time, ILogger<LinkRoleJobRunner> logger)
    {
        _coordinator = coordinator;
        _time = time;
        _logger = logger;
    }

    /// <summary>True while a backfill job runs (agent runs refuse to start meanwhile).</summary>
    public bool IsRunning => Volatile.Read(ref _running) == 1;

    /// <summary>Starts the job and returns at once; null when a seed job, export or another backfill job runs.</summary>
    public LinkRoleJobResponse? TryStart(string mode, Work work)
    {
        if (!_coordinator.TryEnter())
        {
            return null;
        }

        Volatile.Write(ref _running, 1);
        var job = new LinkRoleJobResponse
        {
            Id = Guid.NewGuid().ToString("N"),
            Mode = mode,
            Status = LinkRoleJobResponse.Running,
            StartedAt = _time.GetUtcNow(),
        };

        lock (_gate)
        {
            _job = job;
        }

        _ = Task.Run(() => RunAsync(job, work));

        return Snapshot();
    }

    public LinkRoleJobResponse? Snapshot()
    {
        lock (_gate)
        {
            return _job is null ? null : Copy(_job);
        }
    }

    private async Task RunAsync(LinkRoleJobResponse job, Work work)
    {
        var progress = new Progress(this, job);

        try
        {
            progress.Report($"Started link-roles {job.Mode}");
            var result = await work(progress, CancellationToken.None);

            lock (_gate)
            {
                job.Result = result;
                job.Status = LinkRoleJobResponse.Succeeded;
            }

            progress.Report("Finished");
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Link-roles job {Mode} failed", job.Mode);

            lock (_gate)
            {
                job.Error = e.Message;
                job.Status = LinkRoleJobResponse.Failed;
            }

            progress.Report($"Failed: {e.Message}");
        }
        finally
        {
            lock (_gate)
            {
                job.FinishedAt = _time.GetUtcNow();
            }

            Volatile.Write(ref _running, 0);
            _coordinator.Exit();
        }
    }

    private void Append(LinkRoleJobResponse job, string line)
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

    private static LinkRoleJobResponse Copy(LinkRoleJobResponse job) => new()
    {
        Id = job.Id,
        Mode = job.Mode,
        Status = job.Status,
        StartedAt = job.StartedAt,
        FinishedAt = job.FinishedAt,
        Log = [.. job.Log],
        Error = job.Error,
        Result = job.Result,
    };

    private sealed class Progress : IProgress<string>
    {
        private readonly LinkRoleJobRunner _runner;
        private readonly LinkRoleJobResponse _job;

        public Progress(LinkRoleJobRunner runner, LinkRoleJobResponse job)
        {
            _runner = runner;
            _job = job;
        }

        public void Report(string value) => _runner.Append(_job, value);
    }
}
