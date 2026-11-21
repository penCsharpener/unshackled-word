using FastEndpoints;
using FluentValidation;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Infrastructure.Vocab;
using UnshackledWord.Tooling.WebApi.Auth;
using UnshackledWord.Application.Features.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.VocabEditor.UpdateEntry;

/// <summary>Edits a vocab entry: dictionary form (blank clears it; a changed text becomes origin manual), transliteration and the reviewed mark. Only a changed form bumps ContentVersion (one change row of kind form).</summary>
[RequiresLogin]
public sealed class Endpoint : VocabEditEndpoint<UpdateVocabEntryRequest>
{
    private readonly VocabEntryEditor _editor;

    public Endpoint(VocabEntryEditor editor)
    {
        _editor = editor;
    }

    public override void Configure()
    {
        Put("vocab/entries/{id}");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "Edits a vocab entry: dictionary form (blank clears it; a changed text becomes origin manual), transliteration and the reviewed mark. Only a changed form bumps ContentVersion (one change row of kind form).");
    }

    public override async Task HandleAsync(UpdateVocabEntryRequest req, CancellationToken ct)
    {
        await SendOutcomeAsync(await _editor.UpdateEntryAsync(req.Id, req.DictionaryForm, req.Transliteration, req.Reviewed, UserId, ct), ct);
    }
}

public sealed class Validator : Validator<UpdateVocabEntryRequest>
{
    public Validator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.DictionaryForm).MaximumLength(120);
        RuleFor(x => x.Transliteration).MaximumLength(50);
    }
}
