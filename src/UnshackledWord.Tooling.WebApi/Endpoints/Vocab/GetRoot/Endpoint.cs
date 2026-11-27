using FastEndpoints;
using FluentValidation;
using UnshackledWord.Domain.WebApi.Vocab;
using UnshackledWord.Infrastructure.Vocab;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Vocab.GetRoot;

/// <summary>Anonymous: one root (roadmap 0030.3) with its parent, direct sub-roots and at most 12 family members plus the total. Subject to the general anonymous rate limit.</summary>
public sealed class Endpoint : Endpoint<GetVocabRootRequest, VocabRootDetailResponse>
{
    private readonly VocabRootRepository _repository;

    public Endpoint(VocabRootRepository repository)
    {
        _repository = repository;
    }

    public override void Configure()
    {
        Get("roots/{id}");
        Group<RouteGroupConfig>();
        AllowAnonymous();
        Summary(s => s.Summary = "A vocab root with parent, sub-roots and up to 12 members of its word family (own entry first, then rank) plus the member total. 404 for an unknown id.");
    }

    public override async Task HandleAsync(GetVocabRootRequest req, CancellationToken ct)
    {
        var result = await _repository.GetDetailAsync(req.Id);

        if (result is null)
        {
            await Send.NotFoundAsync(ct);

            return;
        }

        await Send.OkAsync(result, ct);
    }
}

public sealed class Validator : Validator<GetVocabRootRequest>
{
    public Validator()
    {
        RuleFor(x => x.Id).GreaterThanOrEqualTo(1);
    }
}
