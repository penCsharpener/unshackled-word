using FastEndpoints;
using FluentValidation;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Infrastructure.Vocab;
using UnshackledWord.Tooling.WebApi.Auth;
using UnshackledWord.Application.Features.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.VocabEditor.AddMeaning;

/// <summary>Adds a manual, accepted meaning at the end of the entry (409 when the entry has the text already). Bumps ContentVersion.</summary>
[RequiresLogin]
public sealed class Endpoint : VocabEditEndpoint<AddVocabMeaningRequest>
{
    private readonly VocabEntryEditor _editor;

    public Endpoint(VocabEntryEditor editor)
    {
        _editor = editor;
    }

    public override void Configure()
    {
        Post("vocab/entries/{id}/meanings");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "Adds a manual, accepted meaning at the end of the entry (409 when the entry has the text already). Bumps ContentVersion.");
    }

    public override async Task HandleAsync(AddVocabMeaningRequest req, CancellationToken ct)
    {
        await SendOutcomeAsync(await _editor.AddMeaningAsync(req.Id, req.German, UserId, ct), ct);
    }
}

public sealed class Validator : Validator<AddVocabMeaningRequest>
{
    public Validator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.German).NotEmpty().Must(g => g.Trim().Length > 0).MaximumLength(80);
    }
}
