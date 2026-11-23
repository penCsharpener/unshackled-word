using System.Security.Claims;
using FastEndpoints;
using UnshackledWord.Application.Features.TskCuration;
using UnshackledWord.Domain.WebApi.Tsk;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Tsk.SaveWordLink;

/// <summary>Maintainer attaches several links of one source verse to Elb1871 words of that verse (roadmap 0024.5), one transaction, one audit row per link.</summary>
[RequiresLogin]
public sealed class Endpoint : Endpoint<SaveTskWordLinkRequest, object>
{
    private readonly TskCurationService _service;

    public Endpoint(TskCurationService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Put("curation/word");
        Group<RouteGroupConfig>();
        Description(b => b.Produces<TskWordLinkResponse>(200).ProducesProblem(400).Produces<TskProposalsCreatedResponse>(202).ProducesProblem(404));
        Summary(s =>
        {
            s.Summary = "Logged-in user (maintainers write directly, others propose): links 1-100 links (`keys`: `targetStartHebRefId`, `targetEndHebRefId` omitted for one verse) of `sourceHebRefId` to the "
                        + "Elb1871 words `startPosition..endPosition` (PositionInVerse) of that source verse. The quote is rebuilt server-side; origin, rating and "
                        + "comment stay. Returns `{ links: [...] }` in request order.";
            s[400] = "Malformed request, a position the source verse does not have, or a quote over 500 characters.";
            s[202] = "Caller without Admin/Maintainer: stored as a pending proposal (full desired state of the link), nothing curated yet (roadmap 0024.6).";
            s[404] = "A key is no TSK or added link of the source verse; nothing is written.";
        });
    }

    public override async Task HandleAsync(SaveTskWordLinkRequest req, CancellationToken ct)
    {
        HttpContext.Response.Headers.CacheControl = "no-store";
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var outcome = await _service.SaveWordLinkAsync(userId, User.HasWriteRole(), req, ct);

        if (outcome.Status == TskCurationStatus.Proposed)
        {
            await Send.ResponseAsync(outcome.Proposals!, StatusCodes.Status202Accepted, ct);
            return;
        }

        if (outcome.Status == TskCurationStatus.Ok)
        {
            await Send.OkAsync(outcome.Response!, ct);
            return;
        }

        AddError(outcome.Message ?? "The request was refused.");
        await Send.ErrorsAsync(TskCurationHttp.StatusCode(outcome.Status), ct);
    }
}

public sealed class Validator : Validator<SaveTskWordLinkRequest>
{
    public Validator()
    {
        Include(new SaveTskWordLinkRequestValidator());
    }
}
