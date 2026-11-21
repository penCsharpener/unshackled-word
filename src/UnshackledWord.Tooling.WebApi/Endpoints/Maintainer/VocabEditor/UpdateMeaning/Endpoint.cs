using FastEndpoints;
using FluentValidation;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Infrastructure.Vocab;
using UnshackledWord.Tooling.WebApi.Auth;
using UnshackledWord.Application.Features.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.VocabEditor.UpdateMeaning;

/// <summary>Accepts, unaccepts or pins a meaning (only the sent fields change). Unaccepting a derived meaning pins it as well. Only a change of the accepted set bumps ContentVersion.</summary>
[RequiresLogin]
public sealed class Endpoint : VocabEditEndpoint<UpdateVocabMeaningRequest>
{
    private readonly VocabEntryEditor _editor;

    public Endpoint(VocabEntryEditor editor)
    {
        _editor = editor;
    }

    public override void Configure()
    {
        Patch("vocab/entries/{id}/meanings/{meaningId}");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "Accepts, unaccepts or pins a meaning (only the sent fields change). Unaccepting a derived meaning pins it as well. Only a change of the accepted set bumps ContentVersion.");
    }

    public override async Task HandleAsync(UpdateVocabMeaningRequest req, CancellationToken ct)
    {
        await SendOutcomeAsync(await _editor.UpdateMeaningAsync(req.Id, req.MeaningId, req.IsAccepted, req.IsPinned, UserId, ct), ct);
    }
}

public sealed class Validator : Validator<UpdateVocabMeaningRequest>
{
    public Validator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.MeaningId).GreaterThan(0);
        RuleFor(x => x).Must(x => x.IsAccepted is not null || x.IsPinned is not null).WithMessage("Send isAccepted, isPinned or both.");
    }
}
