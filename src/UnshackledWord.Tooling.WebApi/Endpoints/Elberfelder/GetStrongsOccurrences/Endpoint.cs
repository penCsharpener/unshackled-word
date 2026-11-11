using FastEndpoints;
using UnshackledWord.Application.Features.StrongsOccurrences;
using UnshackledWord.Application.Features.StrongsStats;
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
        var result = await _service.GetAsync(req.Strongs, req.Page, req.PageSize, new StrongsOccurrenceFilter(req.Book, req.Rendering), ct);
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
        Include(new GetStrongsOccurrencesRequestValidator());
    }
}
