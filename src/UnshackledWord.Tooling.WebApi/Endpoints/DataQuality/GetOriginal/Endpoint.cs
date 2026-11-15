using FastEndpoints;
using FluentValidation;
using UnshackledWord.Domain.Models.DataQuality;
using UnshackledWord.Domain.WebApi.DataQuality;
using UnshackledWord.Tooling.WebApi.Auth;
using UnshackledWord.Application.Features.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.DataQuality.GetOriginal;

[RequiresLogin]
public sealed class Endpoint : Endpoint<OriginalDetailRequest, OriginalDetailResponse>
{
    private readonly IDqExplorerRepository _explorer;
    private readonly IDqFactsRepository _facts;

    public Endpoint(IDqExplorerRepository explorer, IDqFactsRepository facts)
    {
        _explorer = explorer;
        _facts = facts;
    }

    public override void Configure()
    {
        Get("original/{strongs}");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "One original word (base Strong's number, e.g. G2424 / H0430): counts and its German renderings with flags.");
    }

    public override async Task HandleAsync(OriginalDetailRequest req, CancellationToken ct)
    {
        HttpContext.Response.Headers.CacheControl = "private, max-age=60";
        var state = await _facts.GetStateAsync(ct);
        var detail = await _explorer.GetOriginalAsync(TranslitFolding.NormalizeStrongsKey(req.Strongs)!, state.RefreshedAt, ct);
        if (detail is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(detail, ct);
    }
}

public sealed class Validator : Validator<OriginalDetailRequest>
{
    public Validator()
    {
        RuleFor(x => x.Strongs).Must(s => TranslitFolding.NormalizeStrongsKey(s) is not null).WithMessage("strongs must look like G2424 or H0430.");
    }
}
