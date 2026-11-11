using FastEndpoints;
using UnshackledWord.Application.Features.StrongsStats;
using UnshackledWord.Domain.WebApi.Elberfelder.GetWordStats;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Elberfelder.GetWordStats;

public class Endpoint : Endpoint<GetWordStatsRequest, GetWordStatsResponse>
{
    private readonly IWordStatsService _service;

    public Endpoint(IWordStatsService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Get("wordStats");
        Group<RouteGroupConfig>();
        AllowAnonymous();
        Description(b => b.Produces<GetWordStatsResponse>(200).ProducesProblem(400));
    }

    public override async Task HandleAsync(GetWordStatsRequest req, CancellationToken ct)
    {
        var result = await _service.GetAsync(req.Word, req.Testament, ct);
        if (result is null)
        {
            await Send.ErrorsAsync(400, ct);
            return;
        }

        HttpContext.Response.Headers.CacheControl = "public, max-age=3600";
        await Send.OkAsync(result, ct);
    }
}

public class GetWordStatsValidator : Validator<GetWordStatsRequest>
{
    public GetWordStatsValidator()
    {
        Include(new GetWordStatsRequestValidator());
    }
}
