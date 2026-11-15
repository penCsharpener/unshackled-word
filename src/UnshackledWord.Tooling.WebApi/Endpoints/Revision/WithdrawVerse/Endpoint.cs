using System.Security.Claims;
using FastEndpoints;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Application.Features.Revision;
using UnshackledWord.Domain.WebApi.Revision;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Revision.WithdrawVerse;

[RequiresLogin]
public sealed class Endpoint : Endpoint<WithdrawRevisionRequest>
{
    private readonly IRevisionService _service;

    public Endpoint(IRevisionService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Delete("verse/{RevisionVerseId:int}");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "Ends the active variant without a successor: the level falls back for that verse. Rows are kept; a later save continues with Version + 1.");
        Description(b => b.Produces(204).ProducesProblem(404).ProducesProblem(409));
    }

    public override async Task HandleAsync(WithdrawRevisionRequest req, CancellationToken ct)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        switch (await _service.WithdrawAsync(req.RevisionVerseId, userId, ct))
        {
            case WithdrawRevisionOutcome.NotFound:
                await Send.NotFoundAsync(ct);
                return;
            case WithdrawRevisionOutcome.Conflict:
                AddError("The variant is no longer the active version. Reload and try again.");
                await Send.ErrorsAsync(StatusCodes.Status409Conflict, ct);
                return;
        }

        await Send.NoContentAsync(ct);
    }
}
