using FastEndpoints;
using FluentValidation;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Infrastructure.Vocab;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.VocabRoots.Search;

/// <summary>Root picker of the vocab editor (roadmap 0030.5f).</summary>
[RequiresLogin]
public sealed class Endpoint : Endpoint<SearchVocabRootsRequest, SearchVocabRootsResponse>
{
    private readonly VocabRootRepository _repository;

    public Endpoint(VocabRootRepository repository)
    {
        _repository = repository;
    }

    public override void Configure()
    {
        Get("vocab/roots/search");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "Root candidates of a track by typed text: Greek = active vocab entries by lemma prefix (rootId is the root row of the word's key, null while it has none; send strongsKey then), Hebrew = existing roots by consonants. Empty text lists nothing.");
    }

    public override async Task HandleAsync(SearchVocabRootsRequest req, CancellationToken ct)
    {
        await Send.OkAsync(new SearchVocabRootsResponse { Items = await _repository.SearchAsync(char.ToUpperInvariant(req.Track[0]), req.Q, req.Take) }, ct);
    }
}

public sealed class Validator : Validator<SearchVocabRootsRequest>
{
    public Validator()
    {
        RuleFor(x => x.Track).Must(t => t is "G" or "H" or "g" or "h").WithMessage("track must be G or H.");
        RuleFor(x => x.Q).MaximumLength(50).When(x => x.Q is not null);
        RuleFor(x => x.Take).InclusiveBetween(1, 25);
    }
}
