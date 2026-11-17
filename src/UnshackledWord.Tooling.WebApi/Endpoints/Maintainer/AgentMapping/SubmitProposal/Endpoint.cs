using FastEndpoints;
using FluentValidation;
using UnshackledWord.Application.Features.AgentMapping;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.AgentMapping.SubmitProposal;

[RequiresLogin]
public sealed class Endpoint : Endpoint<SubmitAgentProposalRequest, SubmitAgentProposalResponse>
{
    private readonly IAgentProposalService _service;

    public Endpoint(IAgentProposalService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Post("agent/proposal");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "Submits an answer JSON for a package (roadmap 0016.4). Per verse: accepted, flagged (stored as open proposal with flags) or rejected with messages in local ids (not stored; resubmit). 404 unknown package, 410 expired, 409 stale (verse words changed since the package was built), 400 unusable answer.");
    }

    public override async Task HandleAsync(SubmitAgentProposalRequest req, CancellationToken ct)
    {
        var result = await _service.SubmitAsync(req.PackageId, req.Answer, ct);

        switch (result.Outcome)
        {
            case AgentSubmitOutcome.NotFound:
                AddErrors(result);
                await Send.ErrorsAsync(StatusCodes.Status404NotFound, ct);
                return;
            case AgentSubmitOutcome.Expired:
                AddErrors(result);
                await Send.ErrorsAsync(StatusCodes.Status410Gone, ct);
                return;
            case AgentSubmitOutcome.Stale:
                AddErrors(result);
                await Send.ErrorsAsync(StatusCodes.Status409Conflict, ct);
                return;
            case AgentSubmitOutcome.Invalid:
                AddErrors(result);
                await Send.ErrorsAsync(StatusCodes.Status400BadRequest, ct);
                return;
        }

        HttpContext.Response.Headers.CacheControl = "no-store";
        await Send.OkAsync(result.Response!, ct);
    }

    private void AddErrors(AgentSubmitResult result)
    {
        foreach (var error in result.Errors)
        {
            AddError(error);
        }
    }
}

public sealed class Validator : Validator<SubmitAgentProposalRequest>
{
    public Validator()
    {
        RuleFor(x => x.PackageId).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Answer).Must(a => a.ValueKind is System.Text.Json.JsonValueKind.Object or System.Text.Json.JsonValueKind.String).WithMessage("answer must be the answer JSON object.");
    }
}
