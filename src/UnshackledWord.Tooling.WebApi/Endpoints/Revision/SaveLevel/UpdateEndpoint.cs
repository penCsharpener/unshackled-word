using FastEndpoints;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Domain.WebApi.Revision;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Revision.SaveLevel;

[RequiresLogin]
public sealed class UpdateEndpoint : SaveLevelEndpointBase
{
    private readonly RevisionLevelService _service;

    public UpdateEndpoint(RevisionLevelService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Put("levels/{CurrentLevel:int}");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "Renames or edits a preset; the base (Elb1871) is not editable, the level value may change and keeps all variants, the default can only move to another preset.");
        Description(b => b.Produces<RevisionLevelDetail>(200).ProducesProblem(400).ProducesProblem(404).ProducesProblem(409));
    }

    public override async Task HandleAsync(SaveRevisionLevelRequest req, CancellationToken ct) =>
        await SendResultAsync(await _service.UpdateAsync(req), ct);
}
