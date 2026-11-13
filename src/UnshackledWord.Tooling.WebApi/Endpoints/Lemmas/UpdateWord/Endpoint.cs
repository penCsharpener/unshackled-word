using System.Security.Claims;
using FastEndpoints;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Domain.WebApi.Lemmas;
using UnshackledWord.Tooling.WebApi.Auth;
using UnshackledWord.Tooling.WebApi.Endpoints.Lemmas.Stts;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Lemmas.UpdateWord;

[RequiresLogin]
public sealed class Endpoint : Endpoint<UpdateLemmaWordRequest, LemmaWordRow>
{
    private readonly ILemmaRepository _repository;
    private readonly LemmaSttsGuard _sttsGuard;

    public Endpoint(ILemmaRepository repository, LemmaSttsGuard sttsGuard)
    {
        _repository = repository;
        _sttsGuard = sttsGuard;
    }

    public override void Configure()
    {
        Put("words/{wordId:int}");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "Sets lemma and morphology of one word (creates the morphology row if missing). Direct write; audited. stts: null keeps the stored tag, a tag must exist and fit the part of speech when it or the part of speech changes (400 otherwise).");
    }

    public override async Task HandleAsync(UpdateLemmaWordRequest req, CancellationToken ct)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var change = ToChange(req);
        foreach (var error in await _sttsGuard.CheckAsync([change], ct)) AddError(x => x.Stts!, error);
        ThrowIfAnyErrors();

        var missing = await _repository.ApplyAsync([change], userId, ct);
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
        WordId = r.WordId, Lemma = r.Lemma, PartOfSpeech = r.PartOfSpeech, Stts = r.Stts, Degree = r.Degree, VerbForm = r.VerbForm,
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
