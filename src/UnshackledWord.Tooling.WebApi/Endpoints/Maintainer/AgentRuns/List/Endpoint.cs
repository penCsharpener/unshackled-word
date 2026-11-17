using FastEndpoints;
using UnshackledWord.Application.Features.AgentMapping;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.AgentRuns.List;

[RequiresLogin]
public sealed class Endpoint : Endpoint<ListAgentRunsRequest, ListAgentRunsResponse>
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
        Get("agent-runs");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "The newest agent runs (default 50, max 200) with the same fields as the status endpoint.");
    }

    public override async Task HandleAsync(ListAgentRunsRequest req, CancellationToken ct)
    {
        var limit = Math.Clamp(req.Limit ?? 50, 1, 200);
        var runs = await _runs.ListRunsAsync(limit, ct);

        HttpContext.Response.Headers.CacheControl = "no-store";
        await Send.OkAsync(new ListAgentRunsResponse { Runs = [.. runs.Select(r => AgentRunMapper.ToDto(r, _manager.IsActive(r.Id)))] }, ct);
    }
}
