using FastEndpoints;
using UnshackledWord.Domain.WebApi.Revision;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Revision.GetLevels;

public sealed class Endpoint : EndpointWithoutRequest<List<RevisionLevelEntry>>
{
    private readonly IRevisionLevelRepository _levels;

    public Endpoint(IRevisionLevelRepository levels)
    {
        _levels = levels;
    }

    public override void Configure()
    {
        Get("levels");
        Group<RouteGroupConfig>();
        AllowAnonymous();
        Summary(s => s.Summary = "Literalness levels (slider stops) of the Bible revision, sorted by level; names follow Accept-Language.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var levels = await _levels.GetAllAsync();
        await Send.OkAsync(RevisionLevelMapper.ToEntries(levels, AuthEmailBuilder.ResolveLocale(HttpContext)), ct);
    }
}
