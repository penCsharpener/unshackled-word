using FastEndpoints;
using FluentValidation;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Domain.Models.Grammar;
using UnshackledWord.Domain.WebApi.Lemmas;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Lemmas.BulkOccurrences;

[RequiresLogin]
public sealed class Endpoint : Endpoint<BulkOccurrencesRequest, BulkOccurrencesResponse>
{
    private readonly ILemmaBulkRepository _repository;

    public Endpoint(ILemmaBulkRepository repository)
    {
        _repository = repository;
    }

    public override void Configure()
    {
        Get("bulk/occurrences");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "Paged occurrences of the source lemmas (exact match) with totals per book, lemma and group across all pages.");
    }

    public override async Task HandleAsync(BulkOccurrencesRequest req, CancellationToken ct) =>
        await Send.OkAsync(await _repository.GetOccurrencesAsync(req, ct), ct);
}

public sealed class Validator : Validator<BulkOccurrencesRequest>
{
    public Validator()
    {
        Include(new BulkFilterValidator());
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, LemmaBulkRules.MaxPageSize);
    }
}
