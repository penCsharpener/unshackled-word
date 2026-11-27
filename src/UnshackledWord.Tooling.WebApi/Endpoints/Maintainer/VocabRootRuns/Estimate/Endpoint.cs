using FastEndpoints;
using FluentValidation;
using UnshackledWord.Application.Features.AgentMapping;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Infrastructure.Vocab;
using UnshackledWord.Tooling.WebApi.Auth;
using UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.AgentRuns;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.VocabRootRuns.Estimate;

/// <summary>Dry run of a vocab root agent run (roadmap 0030.4): how many entries, which rank range, tokens and cost. Nothing is written.</summary>
[RequiresLogin]
public sealed class Endpoint : Endpoint<VocabRootRunRangeRequest, VocabRootRunEstimateResponse>
{
    private readonly VocabRootRunService _service;

    public Endpoint(VocabRootRunService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Post("vocab/root-runs/estimate");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "Dry run of a vocab root agent run: entries of the course in the rank range whose main key has no root row (plus prefill rows with checkPrefill; at most limit), batches, estimated tokens and cost. 400 for bad input, 404 for an unknown course or when no entry needs a root.");
    }

    public override async Task HandleAsync(VocabRootRunRangeRequest req, CancellationToken ct)
    {
        var result = await _service.EstimateAsync(req, token: ct);

        if (result.Estimate is null)
        {
            AddError(result.Error ?? "Estimate failed.");
            await Send.ErrorsAsync(AgentRunResponses.StatusOf(result.Outcome), ct);

            return;
        }

        await Send.OkAsync(result.Estimate, ct);
    }
}

public sealed class Validator : Validator<VocabRootRunRangeRequest>
{
    public Validator()
    {
        Include(new VocabRootRunRangeValidator());
    }
}
