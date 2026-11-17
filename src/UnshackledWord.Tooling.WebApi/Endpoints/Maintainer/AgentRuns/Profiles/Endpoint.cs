using FastEndpoints;
using UnshackledWord.Application.Features.AgentMapping;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.AgentRuns.Profiles;

[RequiresLogin]
public sealed class Endpoint : EndpointWithoutRequest<List<AgentProfileDto>>
{
    private readonly IAgentRunService _service;

    public Endpoint(IAgentRunService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Get("agent-runs/profiles");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "The configured provider profiles (AgentMapping:Profiles) with default model, caps and whether they are usable (API key present). Never returns a key.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var profiles = _service.Profiles.Select(p => new AgentProfileDto
        {
            Name = p.Name,
            Kind = p.Kind,
            Model = p.Model,
            Usable = p.IsUsable,
            UnusableReason = p.UnusableReason,
            MaxTokensPerRun = p.MaxTokensPerRun,
            MaxCostPerRun = p.MaxCostPerRun,
            MaxVersesPerPackage = p.MaxVersesPerPackage,
            MaxParallel = p.MaxParallel,
        }).ToList();

        await Send.OkAsync(profiles, ct);
    }
}
