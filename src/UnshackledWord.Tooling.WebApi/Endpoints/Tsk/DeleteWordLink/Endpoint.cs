using System.Security.Claims;
using FastEndpoints;
using UnshackledWord.Application.Features.TskCuration;
using UnshackledWord.Domain.WebApi.Tsk;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Tsk.DeleteWordLink;

/// <summary>Maintainer detaches several links of one source verse from their word (roadmap 0024.5). The keys come in the JSON body.</summary>
[RequiresLogin]
public sealed class Endpoint : Endpoint<DeleteTskWordLinkRequest, TskWordLinkResponse>
{
    private readonly TskCurationService _service;

    public Endpoint(TskCurationService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Delete("curation/word");
        Group<RouteGroupConfig>();
        Description(b => b.Produces<TskWordLinkResponse>(200).ProducesProblem(400).ProducesProblem(403).ProducesProblem(404));
        Summary(s =>
        {
            s.Summary = "Maintainer: removes the word link of 1-100 links (`keys` in the JSON body) of `sourceHebRefId`. A TSK link that is plain afterwards "
                        + "loses its curation row. Returns `{ links: [...] }` in request order.";
            s[400] = "Malformed request.";
            s[403] = "The caller is not Admin or Maintainer.";
            s[404] = "A key is no TSK or added link of the source verse; nothing is written.";
        });
    }

    public override async Task HandleAsync(DeleteTskWordLinkRequest req, CancellationToken ct)
    {
        HttpContext.Response.Headers.CacheControl = "no-store";
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var outcome = await _service.DeleteWordLinkAsync(userId, User.HasWriteRole(), req, ct);

        if (outcome.Status == TskCurationStatus.Ok)
        {
            await Send.OkAsync(outcome.Response!, ct);
            return;
        }

        AddError(outcome.Message ?? "The request was refused.");
        await Send.ErrorsAsync(TskCurationHttp.StatusCode(outcome.Status), ct);
    }
}

public sealed class Validator : Validator<DeleteTskWordLinkRequest>
{
    public Validator()
    {
        Include(new DeleteTskWordLinkRequestValidator());
    }
}
