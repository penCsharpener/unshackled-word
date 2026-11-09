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

        // Mappings change only through tagging or a mapping-seed update.
        HttpContext.Response.Headers.CacheControl = "public, max-age=3600";
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
