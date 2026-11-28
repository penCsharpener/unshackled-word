using FastEndpoints;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Domain.WebApi.Admin;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Seeding.Catalog;
using UnshackledWord.Tooling.WebApi.Auth;
using UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.SeedAdmin;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Admin.Setup.Get;

[RequiresLogin]
public sealed class Endpoint : EndpointWithoutRequest<SetupStatusResponse>
{
    private readonly SetupStatusService _service;
    private readonly SetupRunState _state;
    private readonly SeedJobRunner _runner;

    public Endpoint(SetupStatusService service, SetupRunState state, SeedJobRunner runner)
    {
        _service = service;
        _state = state;
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
        var status = await _service.GetStatusAsync(_state.FailedKeys(), _state.CompletedKeys(), ct);
        var snapshot = _runner.Snapshot();
        status.Job = snapshot is { Operation: SetupOperation.Name } ? snapshot : null;
        status.CurrentStep = status.Job is { Status: SeedJobResponse.Running } ? _state.CurrentStep : null;
        await Send.OkAsync(status, ct);
    }
}
