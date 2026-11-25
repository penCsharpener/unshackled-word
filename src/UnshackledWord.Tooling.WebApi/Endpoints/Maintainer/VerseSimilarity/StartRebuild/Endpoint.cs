using System.Security.Claims;
using FastEndpoints;
using Microsoft.Extensions.DependencyInjection;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Application.Features.VerseSimilarity;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Tooling.WebApi.Auth;
using UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.SeedAdmin;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.VerseSimilarity.StartRebuild;

/// <summary>Runs <see cref="VerseSimilarityRebuildService"/> as a background job on the shared seed job runner (one seed or similarity job at a time).</summary>
[RequiresLogin]
public sealed class Endpoint : Endpoint<StartVerseSimilarityRebuildRequest, StartVerseSimilarityRebuildResponse>
{
    private readonly SeedJobRunner _runner;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IVerseSimilarityRepository _repository;
    private readonly VerseSimilarityRebuildService _service;

    public Endpoint(SeedJobRunner runner, IServiceScopeFactory scopeFactory, IVerseSimilarityRepository repository, VerseSimilarityRebuildService service)
    {
        _runner = runner;
        _scopeFactory = scopeFactory;
        _repository = repository;
        _service = service;
    }

    public override void Configure()
    {
        Post("verse-similarity/rebuild");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "Starts a verse similarity rebuild in the background (parameters optional, defaults from config) and returns the job with status 202; poll GET maintainer/verse-similarity. The run row records the calling user. 409 while a rebuild or another seed job runs, 400 for invalid parameters.");
    }

    public override async Task HandleAsync(StartVerseSimilarityRebuildRequest req, CancellationToken ct)
    {
        var defaults = _service.DefaultParameters;
        var parameters = new VerseSimilarityParameters(
            req.StopDfShare ?? defaults.StopDfShare,
            req.TopK ?? defaults.TopK,
            req.MinScore ?? defaults.MinScore,
            req.PhraseWeight ?? defaults.PhraseWeight);

        // A rebuild of another process (SeedDb) holds the advisory lock: answer 409 now instead of a failed job later.
        // Only looks at pg_locks: taking the lock for a moment could make a SeedDb rebuild starting at that moment skip as busy.
        if (await _repository.IsRebuildLockHeldAsync(ct))
        {
            await ConflictAsync("A verse similarity rebuild is already running.", ct);

            return;
        }

        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var job = _runner.TryStart(StartVerseSimilarityRebuildResponse.Operation, "all", async (progress, token) =>
        {
            // The job outlives this request, so it gets its own scope (and with it its own database connections).
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<VerseSimilarityRebuildService>();

            progress.Report($"Rebuild with StopDfShare {parameters.StopDfShare}, TopK {parameters.TopK}, MinScore {parameters.MinScore}, PhraseWeight {parameters.PhraseWeight}");
            var result = await service.RebuildAsync(parameters, userId, token);

            switch (result.Status)
            {
                case VerseSimilarityRebuildStatus.Succeeded:
                    progress.Report($"Run {result.RunId}: {result.PairCount} pairs, {result.StopWordCount} stop words, {result.DurationMs / 1000.0:F1} s");

                    return new SeedJobResult();
                case VerseSimilarityRebuildStatus.Busy:
                    throw new InvalidOperationException("Another verse similarity rebuild is running.");
                default:
                    throw new InvalidOperationException(result.Error ?? "The rebuild failed.");
            }
        });

        if (job is null)
        {
            await ConflictAsync("Another seed or verse similarity job is already running.", ct);

            return;
        }

        await Send.ResponseAsync(new StartVerseSimilarityRebuildResponse { Job = job }, StatusCodes.Status202Accepted, ct);
    }

    private async Task ConflictAsync(string message, CancellationToken ct)
    {
        AddError(message);
        await Send.ErrorsAsync(StatusCodes.Status409Conflict, ct);
    }
}
