using FastEndpoints;
using FluentValidation;
using UnshackledWord.Application.Features.Revision;
using UnshackledWord.Domain.WebApi.Elberfelder.GetWordDetail;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Revision.GetWordDetail;

public sealed class Request
{
    public int RevisionWordId { get; set; }
}

public sealed class Endpoint : Endpoint<Request, GetWordDetailResponse>
{
    private readonly IRevisionWordDetailService _service;

    public Endpoint(IRevisionWordDetailService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Get("wordDetail/wordId/{RevisionWordId:int}");
        Group<RouteGroupConfig>();
        AllowAnonymous();
        Summary(s => s.Summary = "Word detail of a word of a revision variant (readings from RevisionMapping). Renderings and counts are Elb1871 statistics.");
        Description(b => b.Produces<GetWordDetailResponse>(200).ProducesProblem(404));
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var detail = await _service.GetAsync(req.RevisionWordId, ct);
        if (detail is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        // A mapping save or a new version changes the answer at once.
        HttpContext.Response.Headers.CacheControl = "no-store";
        await Send.OkAsync(detail, ct);
    }
}

public sealed class Validator : Validator<Request>
{
    public Validator()
    {
        RuleFor(x => x.RevisionWordId).GreaterThan(0);
    }
}
