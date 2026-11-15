using System.Security.Claims;
using FastEndpoints;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Application.Features.Revision;
using UnshackledWord.Domain.WebApi.Revision;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Revision.SaveVerseMapping;

[RequiresLogin]
public sealed class Endpoint : Endpoint<SaveRevisionMappingRequest, RevisionVerseMappingResponse>
{
    private readonly IRevisionMappingService _service;

    public Endpoint(IRevisionMappingService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Put("verseMapping/{RevisionVerseId:int}");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "Replaces the mapping of the active variant version and marks it reviewed. 409 when the variant is no longer active.");
        Description(b => b.Produces<RevisionVerseMappingResponse>(200).ProducesProblem(400).ProducesProblem(404).ProducesProblem(409));
    }

    public override async Task HandleAsync(SaveRevisionMappingRequest req, CancellationToken ct)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await _service.SaveAsync(req.RevisionVerseId, req.Links, userId, ct);

        switch (result.Outcome)
        {
            case SaveRevisionMappingOutcome.NotFound:
                await Send.NotFoundAsync(ct);
                return;
            case SaveRevisionMappingOutcome.Invalid:
                foreach (var error in result.Errors)
                {
                    AddError(error);
                }

                await Send.ErrorsAsync(StatusCodes.Status400BadRequest, ct);
                return;
            case SaveRevisionMappingOutcome.Conflict:
                AddError(result.Errors[0]);
                await Send.ErrorsAsync(StatusCodes.Status409Conflict, ct);
                return;
        }

        await Send.OkAsync(result.Mapping!, ct);
    }
}
