using FastEndpoints;
using FluentValidation;
using UnshackledWord.Application.Features.WordDetail;
using UnshackledWord.Domain.WebApi.Elberfelder.GetWordDetail;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Elberfelder.GetWordDetail;

public class Endpoint : Endpoint<GetWordDetailRequest, GetWordDetailResponse>
{
    private readonly IWordDetailService _service;

    public Endpoint(IWordDetailService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Get("wordDetail/wordId/{WordId:int}");
        Group<RouteGroupConfig>();
        AllowAnonymous();
        Description(b => b.Produces<GetWordDetailResponse>(200).ProducesProblem(404));
    }

    public override async Task HandleAsync(GetWordDetailRequest req, CancellationToken ct)
    {
        var detail = await _service.GetAsync(req.WordId, ct);
        if (detail is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        // The word box carries the curated cross references (roadmap 0024.3/0024.5): a maintainer's highlight, hide or word link must show on the
        // next request, not after an hour. "no-cache" lets the browser keep the response but makes it ask again every time; without a validator
        // that is a plain refetch (one word per click, cheap enough). "private": the answer is the same for everyone today, but shared caches
        // gain nothing from a response they must revalidate anyway.
        HttpContext.Response.Headers.CacheControl = "private, no-cache";
        await Send.OkAsync(detail, ct);
    }
}

public class GetWordDetailValidator : Validator<GetWordDetailRequest>
{
    public GetWordDetailValidator()
    {
        RuleFor(x => x.WordId).GreaterThan(0);
    }
}
