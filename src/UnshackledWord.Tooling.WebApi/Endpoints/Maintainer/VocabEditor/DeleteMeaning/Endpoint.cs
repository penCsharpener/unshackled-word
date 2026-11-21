using FastEndpoints;
using FluentValidation;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Infrastructure.Vocab;
using UnshackledWord.Tooling.WebApi.Auth;
using UnshackledWord.Application.Features.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.VocabEditor.DeleteMeaning;

/// <summary>Deletes a manual meaning (409 for a derived one: unaccept it instead; 409 for the last accepted one). Bumps ContentVersion when the meaning was accepted.</summary>
[RequiresLogin]
public sealed class Endpoint : VocabEditEndpoint<DeleteVocabMeaningRequest>
{
    private readonly VocabEntryEditor _editor;

    public Endpoint(VocabEntryEditor editor)
    {
        _editor = editor;
    }

    public override void Configure()
    {
        Delete("vocab/entries/{id}/meanings/{meaningId}");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "Deletes a manual meaning (409 for a derived one: unaccept it instead; 409 for the last accepted one). Bumps ContentVersion when the meaning was accepted.");
    }

    public override async Task HandleAsync(DeleteVocabMeaningRequest req, CancellationToken ct)
    {
        await SendOutcomeAsync(await _editor.DeleteMeaningAsync(req.Id, req.MeaningId, UserId, ct), ct);
    }
}

public sealed class Validator : Validator<DeleteVocabMeaningRequest>
{
    public Validator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.MeaningId).GreaterThan(0);
    }
}
