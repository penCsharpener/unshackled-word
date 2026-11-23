using FastEndpoints;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Infrastructure.Seed.Tsk;
using UnshackledWord.Tooling.WebApi.Auth;
using UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.SeedAdmin;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.TskSeed.Export;

[RequiresLogin]
public sealed class Endpoint : EndpointWithoutRequest<SeedJobResponse>
{
    public const string Operation = "tskExport";

    private readonly SeedJobRunner _runner;
    private readonly IServiceScopeFactory _scopeFactory;

    public Endpoint(SeedJobRunner runner, IServiceScopeFactory scopeFactory)
    {
        _runner = runner;
        _scopeFactory = scopeFactory;
    }

    public override void Configure()
    {
        Post("tsk/export");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "Starts the TSK curation export in the background (roadmap 0024.7): every TskCurations row goes to `assets/seed/tsk/curations.csv` (no user ids, e-mails or names) and the `tsk` part of `assets/seed/manifest.json`. Returns the job with status 202 (operation `tskExport`, kind `tsk`; `result.steps[0].counts.rows`); poll GET maintainer/seed/status. 409 while another seed job runs, 422 when the assets folder is not reachable.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        using (var scope = _scopeFactory.CreateScope())
        {
            var unavailable = scope.ServiceProvider.GetRequiredService<TskCurationSeedService>().CheckAvailable();

            if (unavailable is not null)
            {
                AddError(unavailable);
                await Send.ErrorsAsync(StatusCodes.Status422UnprocessableEntity, ct);

                return;
            }
        }

        var job = _runner.TryStart(Operation, TskCurationSeed.Kind, async (progress, token) =>
        {
            using var scope = _scopeFactory.CreateScope();
            var step = await scope.ServiceProvider.GetRequiredService<TskCurationSeedService>().ExportAsync(null, progress, token);

            return new SeedJobResult { Steps = [step] };
        });

        if (job is null)
        {
            AddError("Another seed job or export is already running.");
            await Send.ErrorsAsync(StatusCodes.Status409Conflict, ct);

            return;
        }

        await Send.ResponseAsync(job, StatusCodes.Status202Accepted, ct);
    }
}
