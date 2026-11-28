using FastEndpoints;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Domain.WebApi.Admin;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Seeding.Catalog;
using UnshackledWord.Tooling.WebApi.Auth;
using UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.SeedAdmin;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Admin.Setup.Run;

[RequiresLogin]
public sealed class Endpoint : Endpoint<SetupRunRequest, SeedJobResponse>
{
    private readonly SetupStatusService _status;
    private readonly SetupRunState _state;
    private readonly SeedStepRunner _runner;
    private readonly SeedJobRunner _jobs;

    public Endpoint(SetupStatusService status, SetupRunState state, SeedStepRunner runner, SeedJobRunner jobs)
    {
        _status = status;
        _state = state;
        _runner = runner;
        _jobs = jobs;
    }

    public override void Configure()
    {
        Post("setup/run");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin);
        Summary(s => s.Summary = "Admin: runs setup steps as one background seed job (operation 'setup', kind = step key, 'group:<name>' or 'all') and returns it with 202; poll GET maintainer/seed/status or GET admin/setup. Body: keys, group or all. 400 when a step is not red / not wired / waits on a step that is not green; 409 while any seed job or export runs.");
        Description(b => b.Produces<SeedJobResponse>(202).ProducesProblem(400).ProducesProblem(403).ProducesProblem(409));
    }

    public override async Task HandleAsync(SetupRunRequest req, CancellationToken ct)
    {
        if (_jobs.Snapshot() is { Status: SeedJobResponse.Running })
        {
            AddError("Another seed job or export is already running.");
            await Send.ErrorsAsync(StatusCodes.Status409Conflict, ct);
            return;
        }

        var states = await _status.GetStatusAsync(_state.FailedKeys(), _state.CompletedKeys(), ct);
        var plan = SeedRunPlanner.Plan(SeedCatalog.Steps, states.Steps, req);
        if (!plan.IsValid)
        {
            AddError(plan.Error!);
            await Send.ErrorsAsync(StatusCodes.Status400BadRequest, ct);
            return;
        }

        var keys = plan.Keys;
        var job = _jobs.TryStart(SetupOperation.Name, plan.Kind, (progress, token) => _runner.RunAsync(keys, progress, token));
        if (job is null)
        {
            AddError("Another seed job or export is already running.");
            await Send.ErrorsAsync(StatusCodes.Status409Conflict, ct);
            return;
        }

        await Send.ResponseAsync(job, StatusCodes.Status202Accepted, ct);
    }
}
