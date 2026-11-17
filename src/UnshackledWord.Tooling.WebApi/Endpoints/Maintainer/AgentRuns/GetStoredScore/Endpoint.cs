using FastEndpoints;
using UnshackledWord.Application.Features.AgentMapping.Scoring;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.AgentRuns.GetStoredScore;

[RequiresLogin]
public sealed class Endpoint : Endpoint<AgentRunScoreRequest, AgentScoreResponse>
{
    private readonly IAgentScoreService _scores;

    public Endpoint(IAgentScoreService scores)
    {
        _scores = scores;
    }

    public override void Configure()
    {
        Get("agent-runs/{runId}/score/stored");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "The score stored with the run by its last scoring (computedAt says when), without recomputing. 404 when the run was never scored.");
    }

    public override async Task HandleAsync(AgentRunScoreRequest req, CancellationToken ct)
    {
        var result = await _scores.GetStoredScoreAsync(req.RunId, ct);
        if (result.Outcome != AgentScoreOutcome.Ok)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        HttpContext.Response.Headers.CacheControl = "no-store";
        await Send.OkAsync(result.Score!, ct);
    }
}
