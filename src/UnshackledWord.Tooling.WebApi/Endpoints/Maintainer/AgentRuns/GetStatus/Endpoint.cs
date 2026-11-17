using FastEndpoints;
using UnshackledWord.Application.Features.AgentMapping;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.AgentRuns.GetStatus;

[RequiresLogin]
public sealed class Endpoint : Endpoint<AgentRunIdRequest, AgentRunDto>
{
    private readonly IAgentRunRepository _runs;
    private readonly AgentRunManager _manager;

    public Endpoint(IAgentRunRepository runs, AgentRunManager manager)
    {
        _runs = runs;
        _manager = manager;
    }

    public override void Configure()
    {
        Get("agent-runs/{runId}");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "Status of one agent run: progress (packages, proposed and flagged verses), totals (tokens, cached tokens, cost, cache rate), caps and the status note. 404 for an unknown run.");
    }

    public override async Task HandleAsync(AgentRunIdRequest req, CancellationToken ct)
    {
        var run = await _runs.GetRunAsync(req.RunId, ct);
        if (run is null)
        {
            await Send.NotFoundAsync(ct);

            return;
        }

        HttpContext.Response.Headers.CacheControl = "no-store";
        await Send.OkAsync(AgentRunMapper.ToDto(run, _manager.IsActive(run.Id)), ct);
    }
}
