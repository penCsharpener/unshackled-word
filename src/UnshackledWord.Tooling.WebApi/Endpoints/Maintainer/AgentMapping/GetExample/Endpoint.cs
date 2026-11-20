using System.Security.Claims;
using FastEndpoints;
using FluentValidation;
using UnshackledWord.Application.Features.AgentMapping;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.AgentMapping.GetExample;

[RequiresLogin]
public sealed class Endpoint : Endpoint<GetAgentExampleRequest, AgentExampleResponse>
{
    private readonly IAgentExampleExporter _exporter;

    public Endpoint(IAgentExampleExporter exporter)
    {
        _exporter = exporter;
    }

    public override void Configure()
    {
        Get("agent/example");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "Turns a stored proposal into a worked example for assets/agent-prompts: a one-verse package (dry run, nothing stored) and the proposal written as an answer against its hints, plus a ready-to-paste Markdown block. Export before the proposal is merged: after a merge the links are fixed and the package no longer shows the hints.");
    }

    public override async Task HandleAsync(GetAgentExampleRequest req, CancellationToken ct)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await _exporter.ExportAsync(req, userId, ct);
        if (result.Example is null)
        {
            AddError(result.Error ?? "Export failed.");
            await Send.ErrorsAsync(result.Outcome switch
            {
                AgentExampleOutcome.NotFound => StatusCodes.Status404NotFound,
                AgentExampleOutcome.Conflict => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status400BadRequest,
            }, ct);

            return;
        }

        HttpContext.Response.Headers.CacheControl = "no-store";
        await Send.OkAsync(result.Example, ct);
    }
}

public sealed class Validator : Validator<GetAgentExampleRequest>
{
    public Validator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.PromptVersion).MaximumLength(50);
    }
}
