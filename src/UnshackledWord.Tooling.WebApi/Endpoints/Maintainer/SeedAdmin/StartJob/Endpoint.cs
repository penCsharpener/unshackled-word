using FastEndpoints;
using Microsoft.Extensions.DependencyInjection;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.SeedAdmin.StartJob;

[RequiresLogin]
public sealed class Endpoint : Endpoint<StartSeedJobRequest, SeedJobResponse>
{
    private readonly SeedJobRunner _runner;
    private readonly IServiceScopeFactory _scopeFactory;

    public Endpoint(SeedJobRunner runner, IServiceScopeFactory scopeFactory)
    {
        _runner = runner;
        _scopeFactory = scopeFactory;
    }

    public override void Configure()
    {
        Post("seed/jobs");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "Starts one seed job in the background (operation export|import|update|rebase|roundTrip, kind all|morphology|mappings|revision) and returns it with status 202; poll GET maintainer/seed/status. Rebase needs confirmBackup=true. 409 while another seed job or export runs, 422 when the assets folder is not reachable.");
    }

    public override async Task HandleAsync(StartSeedJobRequest req, CancellationToken ct)
    {
        var kind = SeedOperationService.ParseKind(req.Kind);

        using (var scope = _scopeFactory.CreateScope())
        {
            var unavailable = scope.ServiceProvider.GetRequiredService<SeedOperationService>().CheckAvailable();

            if (unavailable is not null)
            {
                AddError(unavailable);
                await Send.ErrorsAsync(StatusCodes.Status422UnprocessableEntity, ct);

                return;
            }
        }

        var job = _runner.TryStart(req.Operation, kind.ToString().ToLowerInvariant(), async (progress, token) =>
        {
            // The job outlives this request, so it gets its own scope (and with it its own database connections).
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<SeedOperationService>();

            return await service.RunAsync(req.Operation, kind, req.ConfirmBackup, progress, token);
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
