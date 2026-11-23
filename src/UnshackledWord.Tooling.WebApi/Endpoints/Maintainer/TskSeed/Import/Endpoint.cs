using FastEndpoints;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Infrastructure.Seed.Tsk;
using UnshackledWord.Tooling.WebApi.Auth;
using UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.SeedAdmin;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.TskSeed.Import;

[RequiresLogin]
public sealed class Endpoint : Endpoint<ImportTskCurationsRequest, SeedJobResponse>
{
    public const string Operation = "tskImport";

    private readonly SeedJobRunner _runner;
    private readonly IServiceScopeFactory _scopeFactory;

    public Endpoint(SeedJobRunner runner, IServiceScopeFactory scopeFactory)
    {
        _runner = runner;
        _scopeFactory = scopeFactory;
    }

    public override void Configure()
    {
        Post("tsk/import");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "Starts the TSK curation import in the background from `assets/seed/tsk/curations.csv` (roadmap 0024.7): an existing key takes the file's state when the file's `updatedAt` is newer, keys only in the file are inserted (an `origin = tsk` key without a Tsk row is skipped and listed), keys only in the database are kept unless `mirror` is true (then deleted). Word positions must exist in Elb1871Words; a differing quote is imported and shows as drifted. Every change is audited with source `import` and no user. Returns the job with status 202 (operation `tskImport`, kind `tsk`; `result.steps[0].counts` inserted/updated/unchanged/deleted/skipped, `lists.skipped` with reasons); poll GET maintainer/seed/status. 409 while another seed job runs, 422 when the assets folder is not reachable.");
    }

    public override async Task HandleAsync(ImportTskCurationsRequest req, CancellationToken ct)
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

        var mirror = req.Mirror;
        var job = _runner.TryStart(Operation, TskCurationSeed.Kind, async (progress, token) =>
        {
            using var scope = _scopeFactory.CreateScope();
            var step = await scope.ServiceProvider.GetRequiredService<TskCurationSeedService>().ImportAsync(mirror, null, progress, token);

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
