using FastEndpoints;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Application.Features.Revision;
using UnshackledWord.Domain.WebApi.Revision;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Revision.GetHistory;

[RequiresLogin]
public sealed class Endpoint : Endpoint<RevisionHistoryRequest, RevisionHistoryResponse>
{
    private readonly IRevisionService _service;

    public Endpoint(IRevisionService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Get("verse/bookId/{BibleBookId:int}/chapterId/{ChapterId:int}/verseId/{VerseId:int}/history");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "All versions of the variant of this verse at one level (?level=), newest first, ended versions included.");
        Description(b => b.Produces<RevisionHistoryResponse>(200).ProducesProblem(400).ProducesProblem(404));
    }

    public override async Task HandleAsync(RevisionHistoryRequest req, CancellationToken ct)
    {
        var history = await _service.GetHistoryAsync(req.BibleBookId, req.ChapterId, req.VerseId, req.Level, ct);
        if (history is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(history, ct);
    }
}
