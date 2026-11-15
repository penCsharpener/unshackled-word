using System.Security.Claims;
using FastEndpoints;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Application.Features.Revision;
using UnshackledWord.Domain.WebApi.Revision;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Revision.SaveVerse;

[RequiresLogin]
public sealed class Endpoint : Endpoint<SaveRevisionVerseRequest, SaveRevisionVerseResponse>
{
    private readonly IRevisionService _service;

    public Endpoint(IRevisionService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Post("verse/bookId/{BibleBookId:int}/chapterId/{ChapterId:int}/verseId/{VerseId:int}");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "Saves a new version of the variant of this verse at one level and makes it live. 409 when expectedVersion is not the active version.");
        Description(b => b.Produces<SaveRevisionVerseResponse>(200).ProducesProblem(400).ProducesProblem(404).ProducesProblem(409));
    }

    public override async Task HandleAsync(SaveRevisionVerseRequest req, CancellationToken ct)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await _service.SaveTextAsync(req.BibleBookId, req.ChapterId, req.VerseId, req, userId, ct);

        switch (result.Outcome)
        {
            case SaveRevisionOutcome.NotFound:
                await Send.NotFoundAsync(ct);
                return;
            case SaveRevisionOutcome.Invalid:
                AddError(result.Error!);
                await Send.ErrorsAsync(StatusCodes.Status400BadRequest, ct);
                return;
            case SaveRevisionOutcome.Conflict:
                AddError(result.Error!);
                await Send.ErrorsAsync(StatusCodes.Status409Conflict, ct);
                return;
        }

        await Send.OkAsync(result.Saved!, ct);
    }
}
