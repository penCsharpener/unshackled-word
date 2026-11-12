using FastEndpoints;
using FluentValidation;
using UnshackledWord.Domain.WebApi.Lemmas;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Lemmas.GetVerse;

[RequiresLogin]
public sealed class Endpoint : Endpoint<GetLemmaVerseRequest, LemmaVerseResponse>
{
    private readonly ILemmaRepository _repository;

    public Endpoint(ILemmaRepository repository)
    {
        _repository = repository;
    }

    public override void Configure()
    {
        Get("verse");
        Group<RouteGroupConfig>();
        Summary(s => s.Summary = "One full verse with lemma and morphology codes per word.");
    }

    public override async Task HandleAsync(GetLemmaVerseRequest req, CancellationToken ct)
    {
        var words = await _repository.GetVerseAsync(req.BookId, req.Chapter, req.Verse, ct);
        await Send.OkAsync(new LemmaVerseResponse { BookId = req.BookId, Chapter = req.Chapter, Verse = req.Verse, Words = words }, ct);
    }
}

public sealed class Validator : Validator<GetLemmaVerseRequest>
{
    public Validator()
    {
        RuleFor(x => x.BookId).InclusiveBetween(1, 66);
        RuleFor(x => x.Chapter).GreaterThan(0);
        RuleFor(x => x.Verse).GreaterThan(0);
    }
}
