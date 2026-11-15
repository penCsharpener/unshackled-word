using FastEndpoints;
using FluentValidation;
using UnshackledWord.Domain.WebApi.DataQuality;
using UnshackledWord.Tooling.WebApi.Auth;
using UnshackledWord.Application.Features.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.DataQuality.GetCheck;

[RequiresLogin]
public sealed class Endpoint : Endpoint<CheckRequest, CheckResponse>
{
    private readonly IDqCheckRepository _checks;
    private readonly IDqFactsRepository _facts;

    public Endpoint(IDqCheckRepository checks, IDqFactsRepository facts)
    {
        _checks = checks;
        _facts = facts;
    }

    public override void Configure()
    {
        Get("checks/{code}");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "Findings of one check (U1 U2 U3 C1 M1 M2 M3 M4 L1 L2 L3 L4 L5). Aggregated checks return groups; pass group to expand one into findings.");
    }

    public override async Task HandleAsync(CheckRequest req, CancellationToken ct)
    {
        HttpContext.Response.Headers.CacheControl = "private, max-age=60";
        var state = await _facts.GetStateAsync(ct);
        await Send.OkAsync(await _checks.GetAsync(req, state.RefreshedAt, ct), ct);
    }
}

public sealed class Validator : Validator<CheckRequest>
{
    public Validator()
    {
        Include(new FacetsValidator());
        RuleFor(x => x.Code).Must(DqChecks.IsKnown).WithMessage("Unknown check code.");
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 250);
        RuleFor(x => x.Pos).Matches("^[A-Za-z]{1,8}$").When(x => !string.IsNullOrEmpty(x.Pos));
        RuleFor(x => x.Lang).Must(l => l is null or "" or "G" or "H").WithMessage("lang must be G or H.");
        RuleFor(x => x.Class).MaximumLength(20);
        RuleFor(x => x.Group).MaximumLength(255);
        RuleFor(x => x.Chapter).InclusiveBetween(1, 150).When(x => x.Chapter is not null);
        RuleFor(x => x.MinCount).GreaterThanOrEqualTo(0).When(x => x.MinCount is not null);
    }
}
