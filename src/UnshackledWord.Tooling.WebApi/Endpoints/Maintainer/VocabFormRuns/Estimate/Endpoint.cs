using FastEndpoints;
using FluentValidation;
using UnshackledWord.Application.Features.AgentMapping;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Infrastructure.Vocab;
using UnshackledWord.Tooling.WebApi.Auth;
using UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.AgentRuns;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.VocabFormRuns.Estimate;

/// <summary>Dry run of a dictionary-form agent run (roadmap 0022.13): how many entries, which rank range, tokens and cost. Nothing is written.</summary>
[RequiresLogin]
public sealed class Endpoint : Endpoint<VocabFormRunRangeRequest, VocabFormRunEstimateResponse>
{
    private readonly VocabFormRunService _service;

    public Endpoint(VocabFormRunService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Post("vocab/form-runs/estimate");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "Dry run of a dictionary-form agent run: entries of the course in the rank range whose form is missing or generated (at most limit), batches, estimated tokens and cost. 400 for bad input, 404 for an unknown course or when no entry needs a form.");
    }

    public override async Task HandleAsync(VocabFormRunRangeRequest req, CancellationToken ct)
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

public sealed class Validator : Validator<VocabFormRunRangeRequest>
{
    public Validator()
    {
        Include(new VocabFormRunRangeValidator());
    }
}
