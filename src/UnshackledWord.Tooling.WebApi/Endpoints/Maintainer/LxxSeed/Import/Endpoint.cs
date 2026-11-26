using FastEndpoints;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Infrastructure.Seed.Lxx;
using UnshackledWord.Tooling.WebApi.Auth;
using UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.SeedAdmin;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.LxxSeed.Import;

[RequiresLogin]
public sealed class Endpoint : Endpoint<ImportLxxSeedRequest, SeedJobResponse>
{
    public const string Operation = "lxxSeedImport";

    private readonly SeedJobRunner _runner;
    private readonly IServiceScopeFactory _scopeFactory;

    public Endpoint(SeedJobRunner runner, IServiceScopeFactory scopeFactory)
    {
        _runner = runner;
        _scopeFactory = scopeFactory;
    }

    public override void Configure()
    {
        Post("lxx/seed/import");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "Starts the LXX seed import in the background from `assets/seed/lxx/tags.csv` and `links.csv` (roadmap 0028.8). Lines are anchored to the LXX word by Rahlfs reference, text form, position and hash; a line whose word is gone or whose hash changed is skipped and listed (`anchorMismatch`), the Hebrew side is re-resolved by SourceKey. Merge (default): missing tags and links are inserted, differing or proposed tags become the file's confirmed tag, nothing is deleted. `mirror` true (\"Exakt wie Datei\"): confirmed tags and links the files lack are deleted too; proposals stay untouched in both modes. Imported rows have no user (source from the file, `human` when empty); one LxxTagAudit row `import` per changed verse. Returns the job with status 202 (operation `lxxSeedImport`, kind `lxx`; `result.steps[0].counts`, `lists.skippedRows`); poll GET maintainer/seed/status. 409 while another seed job runs, 422 when the assets folder is not reachable.");
    }

    public override async Task HandleAsync(ImportLxxSeedRequest req, CancellationToken ct)
    {
        using (var scope = _scopeFactory.CreateScope())
        {
            var unavailable = scope.ServiceProvider.GetRequiredService<LxxSeedService>().CheckAvailable();

            if (unavailable is not null)
            {
                AddError(unavailable);
                await Send.ErrorsAsync(StatusCodes.Status422UnprocessableEntity, ct);

                return;
            }
        }

        var mirror = req.Mirror;
        var job = _runner.TryStart(Operation, UnshackledWord.Infrastructure.Seed.Lxx.LxxSeed.Kind, async (progress, token) =>
        {
            using var scope = _scopeFactory.CreateScope();
            var step = await scope.ServiceProvider.GetRequiredService<LxxSeedService>().ImportAsync(mirror, null, progress, token);

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
