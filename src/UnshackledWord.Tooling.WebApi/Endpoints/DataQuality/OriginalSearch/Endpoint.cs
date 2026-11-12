using FastEndpoints;
using FluentValidation;
using UnshackledWord.Domain.WebApi.DataQuality;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.DataQuality.OriginalSearch;

[RequiresLogin]
public sealed class Endpoint : Endpoint<OriginalSearchRequest, OriginalSearchResponse>
{
    private readonly IDqExplorerRepository _explorer;

    public Endpoint(IDqExplorerRepository explorer)
    {
        _explorer = explorer;
    }

    public override void Configure()
    {
        Get("original/search");
        Group<RouteGroupConfig>();
        Summary(s => s.Summary = "Find original words by Strong's number, Greek/Hebrew script, transliteration, Hebrew consonants or English gloss (diacritics ignored).");
    }

    public override async Task HandleAsync(OriginalSearchRequest req, CancellationToken ct)
    {
        HttpContext.Response.Headers.CacheControl = "private, max-age=60";
        await Send.OkAsync(await _explorer.SearchOriginalAsync(req.Q.Trim(), req.Limit, ct), ct);
    }
}

public sealed class Validator : Validator<OriginalSearchRequest>
{
    public Validator()
    {
        RuleFor(x => x.Q).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Limit).InclusiveBetween(1, 100);
    }
}
