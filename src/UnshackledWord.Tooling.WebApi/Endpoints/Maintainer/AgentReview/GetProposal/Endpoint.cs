using System.Security.Claims;
using FastEndpoints;
using FluentValidation;
using UnshackledWord.Application.Features.AgentMapping;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.AgentReview.GetProposal;

[RequiresLogin]
public sealed class Endpoint : Endpoint<AgentProposalIdRequest, AgentProposalDiffResponse>
{
    private readonly IAgentReviewService _service;

    public Endpoint(IAgentReviewService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Get("agent-proposals/{id}");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "One proposed verse against the live verse: unchanged, added and removed links, parts, lemma changes, flags, note, and whether the verse is stale (roadmap 0016.7).");
    }

    public override async Task HandleAsync(AgentProposalIdRequest req, CancellationToken ct)
    {
        var diff = await _service.GetDiffAsync(req.Id, ct);
        if (diff is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        HttpContext.Response.Headers.CacheControl = "no-store";
        await Send.OkAsync(diff, ct);
    }
}
