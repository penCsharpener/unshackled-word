using FastEndpoints;
using FluentValidation;
using UnshackledWord.Application.Features.Revision;
using UnshackledWord.Domain.WebApi.Revision;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Revision.GetVerse;

public sealed class Endpoint : Endpoint<RevisionVerseRouteRequest, RevisionVerseResponse>
{
    private readonly IRevisionService _service;

    public Endpoint(IRevisionService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Get("verse/bookId/{BibleBookId:int}/chapterId/{ChapterId:int}/verseId/{VerseId:int}");
        Group<RouteGroupConfig>();
        AllowAnonymous();
        Summary(s => s.Summary = "Elb1871 text of one verse plus every active revision variant (one per level).");
        Description(b => b.Produces<RevisionVerseResponse>(200).ProducesProblem(404));
    }

    public override async Task HandleAsync(RevisionVerseRouteRequest req, CancellationToken ct)
    {
        var response = await _service.GetVerseAsync(req.BibleBookId, req.ChapterId, req.VerseId, ct);
        if (response is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        // The editor must see a save at once.
        HttpContext.Response.Headers.CacheControl = "no-store";
        await Send.OkAsync(response, ct);
    }
}

public sealed class Validator : Validator<RevisionVerseRouteRequest>
{
    public Validator()
    {
        RuleFor(x => x.BibleBookId).InclusiveBetween(1, 66);
        RuleFor(x => x.ChapterId).InclusiveBetween(1, 999);
        RuleFor(x => x.VerseId).InclusiveBetween(0, 999);
    }
}
