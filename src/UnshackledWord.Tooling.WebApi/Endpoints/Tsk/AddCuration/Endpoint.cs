using System.Security.Claims;
using FastEndpoints;
using UnshackledWord.Application.Features.TskCuration;
using UnshackledWord.Domain.WebApi.Tsk;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Tsk.AddCuration;

/// <summary>
/// Maintainer adds a cross reference TSK lacks (roadmap 0024.4): a <c>TskCurations</c> row with origin <c>added</c> and an audit row.
/// The role check sits in <see cref="TskCurationService"/> like for <c>PUT api/tsk/curation</c>.
/// </summary>
[RequiresLogin]
public sealed class Endpoint : Endpoint<AddTskCurationRequest, object>
{
    private readonly TskCurationService _service;

    public Endpoint(TskCurationService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Post("curation/added");
        Group<RouteGroupConfig>();
        Description(b => b.Produces<TskCurationResponse>(200).ProducesProblem(400).Produces<TskProposalCreatedResponse>(202).ProducesProblem(404).ProducesProblem(409));
        Summary(s =>
        {
            s.Summary = "Logged-in user (maintainers write directly, others propose): adds a cross reference TSK lacks under `sourceHebRefId` (Elberfelder numbering). `reference` is German "
                        + "(\"Röm 8,28-30\", parsed like `api/references/parse`): one verse or a range within one chapter. Optional `comment` (max 500). "
                        + "Returns the new state with `origin` `added`.";
            s[400] = "Malformed request, unparsable target, a target over more than one chapter, a whole chapter or the source verse itself.";
            s[202] = "Caller without Admin/Maintainer: stored as a pending proposal (full desired state of the link), nothing curated yet (roadmap 0024.6).";
            s[404] = "The source verse does not exist in the Elberfelder 1871.";
            s[409] = "TSK already has this link, or it was already added.";
        });
    }

    public override async Task HandleAsync(AddTskCurationRequest req, CancellationToken ct)
    {
        HttpContext.Response.Headers.CacheControl = "no-store";
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var outcome = await _service.AddAsync(userId, User.HasWriteRole(), req, ct);

        if (outcome.Status == TskCurationStatus.Proposed)
        {
            await Send.ResponseAsync(new TskProposalCreatedResponse { Proposal = outcome.Proposal! }, StatusCodes.Status202Accepted, ct);
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

public sealed class Validator : Validator<AddTskCurationRequest>
{
    public Validator()
    {
        Include(new AddTskCurationRequestValidator());
    }
}
