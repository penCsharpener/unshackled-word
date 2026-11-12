using System.Security.Claims;
using FastEndpoints;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Domain.WebApi.Lemmas;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Lemmas.UpdateWord;

[RequiresLogin]
public sealed class Endpoint : Endpoint<UpdateLemmaWordRequest, LemmaWordRow>
{
    private readonly ILemmaRepository _repository;

    public Endpoint(ILemmaRepository repository)
    {
        _repository = repository;
    }

    public override void Configure()
    {
        Put("words/{wordId:int}");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "Sets lemma and morphology of one word (creates the morphology row if missing). Direct write; audited.");
    }

    public override async Task HandleAsync(UpdateLemmaWordRequest req, CancellationToken ct)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var missing = await _repository.ApplyAsync([ToChange(req)], userId, ct);
        if (missing.Count > 0)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var rows = await _repository.GetWordsAsync([req.WordId], ct);
        await Send.OkAsync(rows[0], ct);
    }

    private static LemmaWordChange ToChange(UpdateLemmaWordRequest r) => new()
    {
        WordId = r.WordId, Lemma = r.Lemma, PartOfSpeech = r.PartOfSpeech, Degree = r.Degree, VerbForm = r.VerbForm,
        Tense = r.Tense, Person = r.Person, Number = r.Number, Mood = r.Mood, Case = r.Case, Gender = r.Gender,
    };
}

public sealed class Validator : Validator<UpdateLemmaWordRequest>
{
    public Validator()
    {
        Include(new LemmaEditFieldsValidator());
    }
}
