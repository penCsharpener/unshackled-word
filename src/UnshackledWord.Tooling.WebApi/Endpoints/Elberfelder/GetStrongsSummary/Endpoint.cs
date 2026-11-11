using FastEndpoints;
using UnshackledWord.Application.Features.StrongsStats;
using UnshackledWord.Domain.WebApi.Elberfelder.GetStrongsSummary;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Elberfelder.GetStrongsSummary;

public class Endpoint : Endpoint<GetStrongsSummaryRequest, GetStrongsSummaryResponse>
{
    private readonly IStrongsSummaryService _service;

    public Endpoint(IStrongsSummaryService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Get("strongsSummary/strongs/{Strongs}");
        Group<RouteGroupConfig>();
        AllowAnonymous();
        Description(b => b.Produces<GetStrongsSummaryResponse>(200).ProducesProblem(400).ProducesProblem(404));
    }

    public override async Task HandleAsync(GetStrongsSummaryRequest req, CancellationToken ct)
    {
        var result = await _service.GetAsync(req.Strongs, ct);
        switch (result.Status)
        {
            case StrongsSummaryStatus.Invalid:
                await Send.ErrorsAsync(400, ct);
                return;
            case StrongsSummaryStatus.NotFound:
                await Send.NotFoundAsync(ct);
                return;
        }

        HttpContext.Response.Headers.CacheControl = "public, max-age=3600";
        await Send.OkAsync(result.Response!, ct);
    }
}

public class GetStrongsSummaryValidator : Validator<GetStrongsSummaryRequest>
{
    public GetStrongsSummaryValidator()
    {
        Include(new GetStrongsSummaryRequestValidator());
    }
}
