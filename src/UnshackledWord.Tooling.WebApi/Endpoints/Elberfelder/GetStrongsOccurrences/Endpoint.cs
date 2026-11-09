using FastEndpoints;
using FluentValidation;
using UnshackledWord.Application.Features.StrongsOccurrences;
using UnshackledWord.Domain.Models.Study;
using UnshackledWord.Domain.WebApi.Elberfelder.GetStrongsOccurrences;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Elberfelder.GetStrongsOccurrences;

public class Endpoint : Endpoint<GetStrongsOccurrencesRequest, GetStrongsOccurrencesResponse>
{
    private readonly IStrongsOccurrenceService _service;

    public Endpoint(IStrongsOccurrenceService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Get("strongsOccurrences/strongs/{Strongs}");
        Group<RouteGroupConfig>();
        AllowAnonymous();
        Description(b => b.Produces<GetStrongsOccurrencesResponse>(200).ProducesProblem(400));
    }

    public override async Task HandleAsync(GetStrongsOccurrencesRequest req, CancellationToken ct)
    {
        var result = await _service.GetAsync(req.Strongs, req.Page, req.PageSize, ct);
        if (result is null)
        {
            await Send.ErrorsAsync(400, ct);
            return;
        }

        HttpContext.Response.Headers.CacheControl = "public, max-age=3600";
        await Send.OkAsync(result, ct);
    }
}

public class GetStrongsOccurrencesValidator : Validator<GetStrongsOccurrencesRequest>
{
    public GetStrongsOccurrencesValidator()
    {
        RuleFor(x => x.Strongs)
            .Must(s => StrongsNumber.TryNormalize(s, out _, out _))
            .WithMessage("Strong's number must look like 'G26' or 'H1254A'.");
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, GetStrongsOccurrencesRequest.MaxPageSize);
    }
}
