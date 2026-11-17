using FastEndpoints;
using UnshackledWord.Application.Features.AgentMapping.Scoring;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.AgentRuns.GetScore;

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
        Get("agent-runs/{runId}/score");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "Scores an agent run against the frozen reference set (assets/agent-reference): lemma, STTS, role/morpheme accuracy, link precision/recall, flags, tokens and cost per verse and whether the bulk-merge bar is met. Computed on every call and stored with the run. Query reference picks a file (v1), default the newest. 404 for an unknown run, 409 without a reference file, 422 for an invalid one.");
    }

    public override async Task HandleAsync(AgentRunScoreRequest req, CancellationToken ct)
    {
        var result = await _scores.ScoreRunAsync(req.RunId, req.Reference, ct);
        if (result.Outcome != AgentScoreOutcome.Ok)
        {
            foreach (var error in result.Errors)
            {
                AddError(error);
            }

            await Send.ErrorsAsync(result.Outcome switch
            {
                AgentScoreOutcome.RunNotFound => StatusCodes.Status404NotFound,
                AgentScoreOutcome.NoReference => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status422UnprocessableEntity,
            }, ct);

            return;
        }

        HttpContext.Response.Headers.CacheControl = "no-store";
        await Send.OkAsync(result.Score!, ct);
    }
}
