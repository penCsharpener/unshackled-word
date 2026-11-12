using FastEndpoints;
using FluentValidation;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Domain.Models.Grammar;
using UnshackledWord.Domain.WebApi.Lemmas;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Lemmas.LemmaCounts;

[RequiresLogin]
public sealed class Endpoint : Endpoint<LemmaCountsRequest, LemmaCountsResponse>
{
    private readonly ILemmaBulkRepository _repository;

    public Endpoint(ILemmaBulkRepository repository)
    {
        _repository = repository;
    }

    public override void Configure()
    {
        Get("lemma-counts");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "Lemma autocomplete: case-insensitive prefix match with counts and case variants.");
    }

    public override async Task HandleAsync(LemmaCountsRequest req, CancellationToken ct)
    {
        var items = await _repository.GetLemmaCountsAsync(req.Q, req.Limit, ct);
        await Send.OkAsync(new LemmaCountsResponse { Items = items }, ct);
    }
}

public sealed class Validator : Validator<LemmaCountsRequest>
{
    public Validator()
    {
        RuleFor(x => x.Q).Must(q => LemmaBulkRules.IsValidLemma(q)).WithMessage("q must be 1-255 characters without control characters.");
        RuleFor(x => x.Limit).InclusiveBetween(1, LemmaBulkRules.MaxAutocompleteLimit);
    }
}
