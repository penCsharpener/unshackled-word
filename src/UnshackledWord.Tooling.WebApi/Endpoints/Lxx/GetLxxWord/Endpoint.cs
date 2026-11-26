using FastEndpoints;
using FluentValidation;
using UnshackledWord.Application.Features.Lxx;
using UnshackledWord.Domain.WebApi.Lxx;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Lxx.GetLxxWord;

/// <summary>
/// The word box of one Septuagint word (roadmap 0028.5): form and Rahlfs reference, and lemma, Strong's, parse and TBESG entry when tagged. Public
/// read: anonymous callers see confirmed tags only, Admin/Maintainer also proposed ones (with status and source). A sibling of
/// <c>elberfelder/wordDetail</c>, not an id variant of it: that contract is an Elb1871 (German) word with its original readings.
/// </summary>
public class Endpoint : Endpoint<GetLxxWordRequest, GetLxxWordResponse>
{
    private readonly ILxxWordReadService _service;

    public Endpoint(ILxxWordReadService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Get("word/{LxxWordId:int}");
        Group<RouteGroupConfig>();
        AllowAnonymous();
        Description(b => b.Produces<GetLxxWordResponse>(200).ProducesProblem(404));
    }

    public override async Task HandleAsync(GetLxxWordRequest req, CancellationToken ct)
    {
        var word = await _service.GetAsync(req.LxxWordId, User.HasWriteRole(), ct);
        if (word is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        // Role-dependent, and a confirmed tag must show on the next click (as elberfelder/wordDetail).
        HttpContext.Response.Headers.CacheControl = "private, no-cache";
        await Send.OkAsync(word, ct);
    }
}

public class GetLxxWordValidator : Validator<GetLxxWordRequest>
{
    public GetLxxWordValidator()
    {
        RuleFor(x => x.LxxWordId).GreaterThan(0);
    }
}
