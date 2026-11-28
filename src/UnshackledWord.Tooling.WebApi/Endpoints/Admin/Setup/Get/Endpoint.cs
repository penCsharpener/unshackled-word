using FastEndpoints;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Domain.WebApi.Admin;
using UnshackledWord.Seeding.Catalog;
using UnshackledWord.Tooling.WebApi.Auth;
using UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.SeedAdmin;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Admin.Setup.Get;

[RequiresLogin]
public sealed class Endpoint : EndpointWithoutRequest<SetupStatusResponse>
{
    private readonly SetupStatusService _service;
    private readonly SeedJobRunner _runner;

    public Endpoint(SetupStatusService service, SeedJobRunner runner)
    {
        _service = service;
        _runner = runner;
    }

    public override void Configure()
    {
        Get("setup");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin);
        Summary(s => s.Summary = "Admin: the seed catalog with every step's state from row counts (green/amber/red/grey, missing prerequisites, wired or not) and the running or last setup job.");
        Description(b => b.Produces<SetupStatusResponse>(200).ProducesProblem(403));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        // No job has failed or completed a step through the page yet: runs arrive with 0031.6.
        var status = await _service.GetStatusAsync(new HashSet<string>(), new HashSet<string>(), ct);
        var snapshot = _runner.Snapshot();
        status.Job = snapshot is { Operation: SetupOperation.Name } ? snapshot : null;
        await Send.OkAsync(status, ct);
    }
}
