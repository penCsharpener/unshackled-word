using FastEndpoints;
using FluentValidation;
using UnshackledWord.Domain.WebApi.DataQuality;
using UnshackledWord.Tooling.WebApi.Auth;
using UnshackledWord.Application.Features.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.DataQuality.GermanSearch;

[RequiresLogin]
public sealed class Endpoint : Endpoint<GermanSearchRequest, GermanSearchResponse>
{
    private readonly IDqExplorerRepository _explorer;

    public Endpoint(IDqExplorerRepository explorer)
    {
        _explorer = explorer;
    }

    public override void Configure()
    {
        Get("german/search");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "German lemma candidates by lemma prefix, then by surface-form prefix, with word counts (optional part of speech).");
    }

    public override async Task HandleAsync(GermanSearchRequest req, CancellationToken ct)
    {
        HttpContext.Response.Headers.CacheControl = "private, max-age=60";
        await Send.OkAsync(await _explorer.SearchGermanAsync(req.Q, req.Pos, req.Limit, ct), ct);
    }
}

public sealed class Validator : Validator<GermanSearchRequest>
{
    public Validator()
    {
        RuleFor(x => x.Q).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Pos).Matches("^[A-Za-z]{1,8}$").When(x => !string.IsNullOrEmpty(x.Pos));
        RuleFor(x => x.Limit).InclusiveBetween(1, 100);
    }
}
