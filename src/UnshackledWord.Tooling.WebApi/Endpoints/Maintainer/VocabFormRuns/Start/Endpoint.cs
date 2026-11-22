using System.Security.Claims;
using FastEndpoints;
using FluentValidation;
using UnshackledWord.Application.Features.AgentMapping;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Infrastructure.Vocab;
using UnshackledWord.Tooling.WebApi.Auth;
using UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.AgentRuns;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.VocabFormRuns.Start;

/// <summary>Starts a dictionary-form agent run in the background (roadmap 0022.13).</summary>
[RequiresLogin]
public sealed class Endpoint : Endpoint<StartVocabFormRunRequest, AgentRunDto>
{
    private readonly VocabFormRunService _service;
    private readonly IAgentRunRepository _runs;
    private readonly AgentRunManager _manager;

    public Endpoint(VocabFormRunService service, IAgentRunRepository runs, AgentRunManager manager)
    {
        _service = service;
        _runs = runs;
        _manager = manager;
    }

    public override void Configure()
    {
        Post("vocab/form-runs");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "Starts a dictionary-form agent run (course, fromRank, toRank, limit, profile, model; optional maxTokens/maxCostUsd) and returns the run with status 202. The run is an agent run of mode vocab-form: poll GET maintainer/agent-runs/{runId} (cost, tokens, status note with the counts), cancel and resume with the agent-run routes. It writes DictionaryForm with origin agent under the AI user, only where the form is missing or generated; a changed form bumps ContentVersion like an editor change. 400 for an unusable profile or bad range, 404 for an unknown course or when no entry needs a form.");
    }

    public override async Task HandleAsync(StartVocabFormRunRequest req, CancellationToken ct)
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

public sealed class Validator : Validator<StartVocabFormRunRequest>
{
    public Validator()
    {
        Include(new VocabFormRunRangeValidator());
        RuleFor(x => x.MaxTokens).GreaterThan(0).When(x => x.MaxTokens is not null);
        RuleFor(x => x.MaxCostUsd).GreaterThan(0).When(x => x.MaxCostUsd is not null);
    }
}
