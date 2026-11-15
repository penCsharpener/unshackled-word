using FastEndpoints;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Domain.WebApi.Revision;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Revision.SaveLevel;

[RequiresLogin]
public sealed class CreateEndpoint : SaveLevelEndpointBase
{
    private readonly RevisionLevelService _service;

    public CreateEndpoint(RevisionLevelService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Post("levels");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "Adds a literalness preset (slider stop). 409 when level or code exist, 400 when a second default is requested.");
        Description(b => b.Produces<RevisionLevelDetail>(200).ProducesProblem(400).ProducesProblem(409));
    }

    public override async Task HandleAsync(SaveRevisionLevelRequest req, CancellationToken ct) =>
        await SendResultAsync(await _service.CreateAsync(req), ct);
}
