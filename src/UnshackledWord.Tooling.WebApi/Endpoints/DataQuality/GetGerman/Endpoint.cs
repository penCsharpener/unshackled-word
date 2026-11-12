using FastEndpoints;
using FluentValidation;
using UnshackledWord.Domain.WebApi.DataQuality;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.DataQuality.GetGerman;

[RequiresLogin]
public sealed class Endpoint : Endpoint<GermanDetailRequest, GermanDetailResponse>
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
        Get("german");
        Group<RouteGroupConfig>();
        Summary(s => s.Summary = "One German lemma (optional PoS): the original words behind it (incl. added / none) with flags, and its surface forms.");
    }

    public override async Task HandleAsync(GermanDetailRequest req, CancellationToken ct)
    {
        HttpContext.Response.Headers.CacheControl = "private, max-age=60";
        var state = await _facts.GetStateAsync(ct);
        await Send.OkAsync(await _explorer.GetGermanAsync(req.Lemma, req.Pos, state.RefreshedAt, ct), ct);
    }
}

public sealed class Validator : Validator<GermanDetailRequest>
{
    public Validator()
    {
        RuleFor(x => x.Lemma).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Pos).Matches("^[A-Za-z]{1,8}$").When(x => !string.IsNullOrEmpty(x.Pos));
    }
}
