using FastEndpoints;
using UnshackledWord.Application.Features.GreekEditions;
using UnshackledWord.Domain.WebApi.GreekEditions.GetGreekEditions;

namespace UnshackledWord.Tooling.WebApi.Endpoints.GreekEditions.GetGreekEditions;

public class Endpoint : EndpointWithoutRequest<GetGreekEditionsResponse>
{
    private readonly IGreekEditionService _service;

    public Endpoint(IGreekEditionService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Get("greekEditions");
        AllowAnonymous();
        Description(b => b.Produces<GetGreekEditionsResponse>(200));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        // The catalog only changes with a migration or a new STEP release.
        HttpContext.Response.Headers.CacheControl = "public, max-age=86400";
        await Send.OkAsync(await _service.GetCatalogAsync(ct), ct);
    }
}
