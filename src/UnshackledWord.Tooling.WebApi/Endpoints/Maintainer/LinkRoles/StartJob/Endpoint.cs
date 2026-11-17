using System.Security.Claims;
using FastEndpoints;
using Microsoft.Extensions.DependencyInjection;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Application.Features.LinkRoles.Backfill;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.LinkRoles.StartJob;

[RequiresLogin]
public sealed class Endpoint : Endpoint<StartLinkRoleJobRequest, LinkRoleJobResponse>
{
    private readonly LinkRoleJobRunner _runner;
    private readonly IServiceScopeFactory _scopeFactory;

    public Endpoint(LinkRoleJobRunner runner, IServiceScopeFactory scopeFactory)
    {
        _runner = runner;
        _scopeFactory = scopeFactory;
    }

    public override void Configure()
    {
        Post("link-roles/jobs");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "Starts the link-roles backfill (roadmap 0018.4) in the background: mode dryRun (report only), apply (one change set) or undo (latest change set); poll GET maintainer/link-roles/status. 409 while an agent run is active or a seed job, export or backfill job runs.");
    }

    public override async Task HandleAsync(StartLinkRoleJobRequest req, CancellationToken ct)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        using (var scope = _scopeFactory.CreateScope())
        {
            if (await scope.ServiceProvider.GetRequiredService<LinkRoleBackfillService>().CheckBlockedAsync(ct) is { } blocked)
            {
                AddError(blocked);
                await Send.ErrorsAsync(StatusCodes.Status409Conflict, ct);

                return;
            }
        }

        var job = _runner.TryStart(req.Mode, async (progress, token) =>
        {
            // The job outlives this request, so it gets its own scope (and with it its own database connections).
            using var scope = _scopeFactory.CreateScope();

            return await scope.ServiceProvider.GetRequiredService<LinkRoleBackfillService>().RunAsync(req.Mode, userId, progress, token);
        });

        if (job is null)
        {
            AddError("A seed job, seed export or link-roles job is already running.");
            await Send.ErrorsAsync(StatusCodes.Status409Conflict, ct);

            return;
        }

        await Send.ResponseAsync(job, StatusCodes.Status202Accepted, ct);
    }
}
