using FastEndpoints;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Application.Features.VerseSimilarity;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Tooling.WebApi.Auth;
using UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.SeedAdmin;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.VerseSimilarity.GetStatus;

[RequiresLogin]
public sealed class Endpoint : EndpointWithoutRequest<VerseSimilarityStatusResponse>
{
    public const int RunCount = 5;

    private readonly SeedJobRunner _runner;
    private readonly IVerseSimilarityRepository _repository;
    private readonly VerseSimilarityRebuildService _service;

    public Endpoint(SeedJobRunner runner, IVerseSimilarityRepository repository, VerseSimilarityRebuildService service)
    {
        _runner = runner;
        _repository = repository;
        _service = service;
    }

    public override void Configure()
    {
        Get("verse-similarity");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "The configured defaults, the last five verse similarity runs (newest first, with parameters, counts, duration, error and the raw evaluation JSON), whether a rebuild runs, and the running or last job of this process (poll this while a rebuild runs).");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var runs = await _repository.GetRecentRunsAsync(RunCount, ct);
        var job = _runner.Snapshot();
        var jobOfOurs = job is { Operation: StartVerseSimilarityRebuildResponse.Operation } ? job : null;

        await Send.OkAsync(new VerseSimilarityStatusResponse
        {
            Defaults = VerseSimilarityMapper.ToResponse(_service.DefaultParameters),
            Runs = runs.Select(VerseSimilarityMapper.ToResponse).ToList(),
            Running = jobOfOurs?.Status == SeedJobResponse.Running || await IsLockHeldAsync(ct),
            Job = jobOfOurs
        }, ct);
    }

    /// <summary>A rebuild of another process (SeedDb verb) holds the advisory lock.</summary>
    private async Task<bool> IsLockHeldAsync(CancellationToken ct)
    {
        var rebuildLock = await _repository.TryAcquireRebuildLockAsync(ct);

        if (rebuildLock is null)
        {
            return true;
        }

        await rebuildLock.DisposeAsync();

        return false;
    }
}
