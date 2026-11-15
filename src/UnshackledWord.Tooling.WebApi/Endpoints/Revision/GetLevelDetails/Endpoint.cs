using FastEndpoints;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Domain.WebApi.Revision;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Revision.GetLevelDetails;

[RequiresLogin]
public sealed class Endpoint : EndpointWithoutRequest<List<RevisionLevelDetail>>
{
    private readonly RevisionLevelService _service;

    public Endpoint(RevisionLevelService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Get("levels/manage");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "Presets with both names, descriptions and the number of stored variant versions per level, for the maintainer's level management.");
    }

    public override async Task HandleAsync(CancellationToken ct) => await Send.OkAsync(await _service.GetDetailsAsync(), ct);
}
