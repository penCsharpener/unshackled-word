using FastEndpoints;
using FluentValidation;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Infrastructure.Vocab;
using UnshackledWord.Tooling.WebApi.Auth;
using UnshackledWord.Application.Features.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.VocabEditor.MoveKey;

/// <summary>Moves a Strong's key to an existing entry of the same track (body targetEntryId) or into a new entry (no target). The source gets a split row (new entry) or a keys row (existing; the target gets one too). Occurrences are summed again and the ranks recomputed.</summary>
[RequiresLogin]
public sealed class Endpoint : VocabEditEndpoint<MoveVocabKeyRequest>
{
    private readonly VocabEntryEditor _editor;

    public Endpoint(VocabEntryEditor editor)
    {
        _editor = editor;
    }

    public override void Configure()
    {
        Post("vocab/entries/{id}/keys/{key}/move");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "Moves a Strong's key to an existing entry of the same track (body targetEntryId) or into a new entry (no target). The source gets a split row (new entry) or a keys row (existing; the target gets one too). Occurrences are summed again and the ranks recomputed.");
    }

    public override async Task HandleAsync(MoveVocabKeyRequest req, CancellationToken ct)
    {
        await SendOutcomeAsync(await _editor.MoveKeyAsync(req.Id, req.Key.ToUpperInvariant(), req.TargetEntryId, UserId, ct), ct);
    }
}

public sealed class Validator : Validator<MoveVocabKeyRequest>
{
    public Validator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Key).Matches(@"^[GgHh]\d{1,5}[A-Za-z]?$").WithMessage("key must look like G3056 or H1254B.");
        RuleFor(x => x.TargetEntryId).GreaterThan(0).When(x => x.TargetEntryId is not null);
    }
}
