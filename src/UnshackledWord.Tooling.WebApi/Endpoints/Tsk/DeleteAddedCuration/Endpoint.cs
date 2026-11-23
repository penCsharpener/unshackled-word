using System.Security.Claims;
using FastEndpoints;
using UnshackledWord.Application.Features.TskCuration;
using UnshackledWord.Domain.WebApi.Tsk;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Tsk.DeleteAddedCuration;

/// <summary>Maintainer removes an added cross reference (roadmap 0024.4) with an audit row. The key comes in the JSON body like for the PUT.</summary>
[RequiresLogin]
public sealed class Endpoint : Endpoint<DeleteTskCurationRequest>
{
    private readonly TskCurationService _service;

    public Endpoint(TskCurationService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Delete("curation/added");
        Group<RouteGroupConfig>();
        Description(b => b.Produces(204).ProducesProblem(400).ProducesProblem(403).ProducesProblem(404));
        Summary(s =>
        {
            s.Summary = "Maintainer: deletes an added cross reference (`origin` `added`), keyed by `sourceHebRefId`, `targetStartHebRefId` and "
                        + "`targetEndHebRefId` (omitted for one verse) in the JSON body. Answers 204.";
            s[400] = "Malformed key, or the key is a TSK link (hide it instead).";
            s[403] = "The caller is not Admin or Maintainer.";
            s[404] = "No added link with this key.";
        });
    }

    public override async Task HandleAsync(DeleteTskCurationRequest req, CancellationToken ct)
    {
        HttpContext.Response.Headers.CacheControl = "no-store";
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var outcome = await _service.DeleteAddedAsync(userId, User.HasWriteRole(), req, ct);

        if (outcome.Status == TskCurationStatus.Ok)
        {
            await Send.NoContentAsync(ct);
            return;
        }

        AddError(outcome.Message ?? "The request was refused.");
        await Send.ErrorsAsync(TskCurationHttp.StatusCode(outcome.Status), ct);
    }
}

public sealed class Validator : Validator<DeleteTskCurationRequest>
{
    public Validator()
    {
        Include(new DeleteTskCurationRequestValidator());
    }
}
