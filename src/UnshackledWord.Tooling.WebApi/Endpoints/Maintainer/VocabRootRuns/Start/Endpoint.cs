using System.Security.Claims;
using FastEndpoints;
using FluentValidation;
using UnshackledWord.Application.Features.AgentMapping;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Infrastructure.Vocab;
using UnshackledWord.Tooling.WebApi.Auth;
using UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.AgentRuns;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.VocabRootRuns.Start;

/// <summary>Starts a vocab root agent run in the background (roadmap 0030.4).</summary>
[RequiresLogin]
public sealed class Endpoint : Endpoint<StartVocabRootRunRequest, AgentRunDto>
{
    private readonly VocabRootRunService _service;
    private readonly IAgentRunRepository _runs;
    private readonly AgentRunManager _manager;

    public Endpoint(VocabRootRunService service, IAgentRunRepository runs, AgentRunManager manager)
    {
        _service = service;
        _runs = runs;
        _manager = manager;
    }

    public override void Configure()
    {
        Post("vocab/root-runs");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "Starts a vocab root agent run (course, fromRank, toRank, limit, profile, model, checkPrefill; optional maxTokens/maxCostUsd) and returns the run with status 202. The run is an agent run of mode vocab-root: poll GET maintainer/agent-runs/{runId} (cost, tokens, status note with the counts), cancel and resume with the agent-run routes. It writes VocabStrongsRoots rows with origin agent and the run id where the main key has no row (prefill rows too with checkPrefill, listed in the run report; manual rows never); roots that do not exist yet are created. GET maintainer/vocab/root-runs/{runId}/report lists rejected, flagged and replaced entries. 400 for an unusable profile or bad range, 404 for an unknown course or when no entry needs a root.");
    }

    public override async Task HandleAsync(StartVocabRootRunRequest req, CancellationToken ct)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var created = await _service.CreateRunAsync(req, userId, ct);

        if (created.Outcome != AgentRunPrepareOutcome.Ok)
        {
            AddError(created.Error ?? "Could not start the run.");
            await Send.ErrorsAsync(AgentRunResponses.StatusOf(created.Outcome), ct);

            return;
        }

        _manager.TryLaunch(created.RunId);
        var run = await _runs.GetRunAsync(created.RunId, ct);

        await Send.ResponseAsync(AgentRunMapper.ToDto(run!, true), StatusCodes.Status202Accepted, ct);
    }
}

public sealed class Validator : Validator<StartVocabRootRunRequest>
{
    public Validator()
    {
        Include(new VocabRootRunRangeValidator());
        RuleFor(x => x.MaxTokens).GreaterThan(0).When(x => x.MaxTokens is not null);
        RuleFor(x => x.MaxCostUsd).GreaterThan(0).When(x => x.MaxCostUsd is not null);
    }
}
