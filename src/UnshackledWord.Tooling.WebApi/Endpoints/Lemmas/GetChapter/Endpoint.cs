using FastEndpoints;
using FluentValidation;
using UnshackledWord.Domain.WebApi.Lemmas;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Lemmas.GetChapter;

[RequiresLogin]
public sealed class Endpoint : Endpoint<GetLemmaChapterRequest, LemmaChapterResponse>
{
    private readonly ILemmaRepository _repository;

    public Endpoint(ILemmaRepository repository)
    {
        _repository = repository;
    }

    public override void Configure()
    {
        Get("chapter");
        Group<RouteGroupConfig>();
        Summary(s => s.Summary = "All Elb1871 words of a chapter with lemma and morphology codes (left join; hasMorphology=false when no row).");
    }

    public override async Task HandleAsync(GetLemmaChapterRequest req, CancellationToken ct)
    {
        var words = await _repository.GetChapterAsync(req.BookId, req.Chapter, ct);
        await Send.OkAsync(new LemmaChapterResponse { BookId = req.BookId, Chapter = req.Chapter, Words = words }, ct);
    }
}

public sealed class Validator : Validator<GetLemmaChapterRequest>
{
    public Validator()
    {
        RuleFor(x => x.BookId).InclusiveBetween(1, 66);
        RuleFor(x => x.Chapter).GreaterThan(0);
    }
}
