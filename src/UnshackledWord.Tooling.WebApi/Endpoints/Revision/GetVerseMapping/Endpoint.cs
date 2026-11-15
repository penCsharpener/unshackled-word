using FastEndpoints;
using FluentValidation;
using UnshackledWord.Application.Features.Revision;
using UnshackledWord.Domain.WebApi.Revision;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Revision.GetVerseMapping;

public sealed class Endpoint : Endpoint<RevisionVerseMappingRouteRequest, RevisionVerseMappingResponse>
{
    private readonly IRevisionMappingService _service;

    public Endpoint(IRevisionMappingService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Get("verseMapping/{RevisionVerseId:int}");
        Group<RouteGroupConfig>();
        AllowAnonymous();
        Summary(s => s.Summary = "German words (RevisionWords ids), original words and links of one variant version, in the shape of elberfelder/verseMapping.");
        Description(b => b.Produces<RevisionVerseMappingResponse>(200).ProducesProblem(404));
    }

    public override async Task HandleAsync(RevisionVerseMappingRouteRequest req, CancellationToken ct)
    {
        var response = await _service.GetAsync(req.RevisionVerseId, ct);
        if (response is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        // The mapping form must see a save at once.
        HttpContext.Response.Headers.CacheControl = "no-store";
        await Send.OkAsync(response, ct);
    }
}

public sealed class Validator : Validator<RevisionVerseMappingRouteRequest>
{
    public Validator()
    {
        RuleFor(x => x.RevisionVerseId).GreaterThan(0);
    }
}
