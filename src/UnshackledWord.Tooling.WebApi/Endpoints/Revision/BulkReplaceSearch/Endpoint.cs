using FastEndpoints;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Domain.WebApi.Revision;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Revision.BulkReplaceSearch;

[RequiresLogin]
public sealed class Endpoint : Endpoint<BulkReplaceSearchRequest, BulkReplaceSearchResponse>
{
    private readonly BulkReplaceSearchService _service;

    public Endpoint(BulkReplaceSearchService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Post("bulkReplace/search");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "Finds every occurrence of a German word in the text of the source revision (own variant else Elb1871), with context, Strong's facets and target flags; a POST because of the body, nothing is written.");
    }

    public override async Task HandleAsync(BulkReplaceSearchRequest req, CancellationToken ct)
    {
        var result = await _service.SearchAsync(req);

        if (result.Outcome != BulkReplaceSearchOutcome.Ok)
        {
            AddError(result.Error!);
            await Send.ErrorsAsync(StatusCodes.Status400BadRequest, ct);
            return;
        }

        await Send.OkAsync(result.Response!, ct);
    }
}
