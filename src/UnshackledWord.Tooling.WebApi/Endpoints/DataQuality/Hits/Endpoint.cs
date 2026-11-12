using FastEndpoints;
using FluentValidation;
using UnshackledWord.Domain.Models.DataQuality;
using UnshackledWord.Domain.WebApi.DataQuality;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.DataQuality.Hits;

[RequiresLogin]
public sealed class Endpoint : Endpoint<HitsRequest, HitsResponse>
{
    private readonly IDqExplorerRepository _explorer;

    public Endpoint(IDqExplorerRepository explorer)
    {
        _explorer = explorer;
    }

    public override void Configure()
    {
        Get("hits");
        Group<RouteGroupConfig>();
        Summary(s => s.Summary = "Verse hits of an explorer filter (strongs and / or lemma [+ pos]); strongs may be added | none together with lemma. Each hit carries ~5 words of context.");
    }

    public override async Task HandleAsync(HitsRequest req, CancellationToken ct)
    {
        HttpContext.Response.Headers.CacheControl = "private, max-age=60";
        var strongs = req.Strongs is "added" or "none" or null or "" ? req.Strongs : TranslitFolding.NormalizeStrongsKey(req.Strongs);
        await Send.OkAsync(await _explorer.GetHitsAsync(new HitsRequest { Strongs = strongs, Lemma = req.Lemma, Pos = req.Pos, Page = req.Page, PageSize = req.PageSize }, ct), ct);
    }
}

public sealed class Validator : Validator<HitsRequest>
{
    public Validator()
    {
        RuleFor(x => x).Must(x => !string.IsNullOrWhiteSpace(x.Strongs) || !string.IsNullOrWhiteSpace(x.Lemma))
            .WithName("strongs").WithMessage("One of strongs or lemma is required.");
        RuleFor(x => x.Strongs).Must(s => s is "added" or "none" || TranslitFolding.NormalizeStrongsKey(s) is not null)
            .When(x => !string.IsNullOrEmpty(x.Strongs)).WithMessage("strongs must look like G2424, H0430, added or none.");
        RuleFor(x => x).Must(x => x.Strongs is not ("added" or "none") || !string.IsNullOrWhiteSpace(x.Lemma))
            .WithName("lemma").WithMessage("strongs=added|none needs a lemma.");
        RuleFor(x => x.Lemma).MaximumLength(255);
        RuleFor(x => x.Pos).Matches("^[A-Za-z]{1,8}$").When(x => !string.IsNullOrEmpty(x.Pos));
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
