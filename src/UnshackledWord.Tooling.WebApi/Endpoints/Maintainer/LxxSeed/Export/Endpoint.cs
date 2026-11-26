using FastEndpoints;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Infrastructure.Seed.Lxx;
using UnshackledWord.Tooling.WebApi.Auth;
using UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.SeedAdmin;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.LxxSeed.Export;

[RequiresLogin]
public sealed class Endpoint : EndpointWithoutRequest<SeedJobResponse>
{
    public const string Operation = "lxxSeedExport";

    private readonly SeedJobRunner _runner;
    private readonly IServiceScopeFactory _scopeFactory;

    public Endpoint(SeedJobRunner runner, IServiceScopeFactory scopeFactory)
    {
        _runner = runner;
        _scopeFactory = scopeFactory;
    }

    public override void Configure()
    {
        Post("lxx/seed/export");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "Starts the LXX seed export in the background (roadmap 0028.8): every confirmed LxxWordTags row goes to `assets/seed/lxx/tags.csv`, every confirmed LxxHebrewLinks row to `assets/seed/lxx/links.csv` (anchored by Rahlfs reference, text form, position and word hash; Hebrew side as SourceKey; no Greek text, no user ids) and the `lxx` part of `assets/seed/manifest.json`. Proposals are never exported. Returns the job with status 202 (operation `lxxSeedExport`, kind `lxx`; `result.steps[0].counts` tags/links); poll GET maintainer/seed/status. 409 while another seed job runs, 422 when the assets folder is not reachable.");
    }

    public override async Task HandleAsync(CancellationToken ct)
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

        var job = _runner.TryStart(Operation, UnshackledWord.Infrastructure.Seed.Lxx.LxxSeed.Kind, async (progress, token) =>
        {
            using var scope = _scopeFactory.CreateScope();
            var step = await scope.ServiceProvider.GetRequiredService<LxxSeedService>().ExportAsync(null, progress, token);

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
