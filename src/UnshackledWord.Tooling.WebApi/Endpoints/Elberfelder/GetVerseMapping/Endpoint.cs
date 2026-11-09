using FastEndpoints;
using FluentValidation;
using UnshackledWord.Application.Features.VerseMapping;
using UnshackledWord.Domain.WebApi.Elberfelder.GetVerseMapping;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Elberfelder.GetVerseMapping;

public class Endpoint : Endpoint<GetVerseMappingRequest, GetVerseMappingResponse>
{
    private readonly IVerseMappingService _service;

    public Endpoint(IVerseMappingService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Get("verseMapping/bookId/{BibleBookId:int}/chapterId/{ChapterId:int}/verseId/{VerseId:int}");
        Group<RouteGroupConfig>();
        AllowAnonymous();
        Description(b => b.Produces<GetVerseMappingResponse>(200).ProducesProblem(400).ProducesProblem(404));
    }

    public override async Task HandleAsync(GetVerseMappingRequest req, CancellationToken ct)
    {
        var mapping = await _service.GetAsync(req.BibleBookId, req.ChapterId, req.VerseId, ct);
        if (mapping is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        // The mapping is editable, so it must never be cached.
        HttpContext.Response.Headers.CacheControl = "no-store";
        await Send.OkAsync(mapping, ct);
    }
}

public class GetVerseMappingValidator : Validator<GetVerseMappingRequest>
{
    public GetVerseMappingValidator()
    {
        RuleFor(x => x.BibleBookId).InclusiveBetween(1, 66);
        RuleFor(x => x.ChapterId).GreaterThan(0);
        RuleFor(x => x.VerseId).GreaterThan(0);
    }
}
