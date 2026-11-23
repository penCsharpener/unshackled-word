using System.Security.Claims;
using FastEndpoints;
using UnshackledWord.Application.Features.TskCuration;
using UnshackledWord.Domain.WebApi.Tsk;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Tsk.SaveCuration;

/// <summary>
/// Maintainer sets rating and comment of one TSK link (roadmap 0024.3), written with an audit row. The role check sits in
/// <see cref="TskCurationService"/> because suggestions by other users take this route from 0024.6 on.
/// </summary>
[RequiresLogin]
public sealed class Endpoint : Endpoint<SaveTskCurationRequest, TskCurationResponse>
{
    private readonly TskCurationService _service;

    public Endpoint(TskCurationService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Put("curation");
        Group<RouteGroupConfig>();
        Description(b => b.Produces<TskCurationResponse>(200).ProducesProblem(400).ProducesProblem(403).ProducesProblem(404));
        Summary(s =>
        {
            s.Summary = "Maintainer: sets `rating` (`highlight`, `hide` or null) and `comment` (plain text, max 500, empty removes it) of one TSK or added link, "
                        + "keyed by source verse and target verse/range in Elberfelder numbering (`targetEndHebRefId` omitted for one verse). "
                        + "A TSK link back at neutral without comment loses its curation row; an added link keeps it. Returns the resulting state.";
            s[403] = "The caller is not Admin or Maintainer.";
            s[404] = "Neither TSK nor the added links have a link with this key.";
        });
    }

    public override async Task HandleAsync(SaveTskCurationRequest req, CancellationToken ct)
    {
        HttpContext.Response.Headers.CacheControl = "no-store";
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var outcome = await _service.SaveAsync(userId, User.HasWriteRole(), req, ct);

        if (outcome.Status == TskCurationStatus.Ok)
        {
            await Send.OkAsync(outcome.Response!, ct);
            return;
        }

        AddError(outcome.Message ?? "The request was refused.");
        await Send.ErrorsAsync(TskCurationHttp.StatusCode(outcome.Status), ct);
    }
}

public sealed class Validator : Validator<SaveTskCurationRequest>
{
    public Validator()
    {
        Include(new SaveTskCurationRequestValidator());
    }
}
