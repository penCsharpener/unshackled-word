using FastEndpoints;
using FluentValidation;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Infrastructure.Vocab;
using UnshackledWord.Tooling.WebApi.Auth;
using UnshackledWord.Application.Features.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.VocabEditor.Merge;

/// <summary>Merges the entry into the target entry (same track): keys and the meanings the target lacks move over, both get a merge row, the source is deleted. The card side comes in 0022.8b.</summary>
[RequiresLogin]
public sealed class Endpoint : VocabEditEndpoint<MergeVocabEntryRequest>
{
    private readonly VocabEntryEditor _editor;

    public Endpoint(VocabEntryEditor editor)
    {
        _editor = editor;
    }

    public override void Configure()
    {
        Post("vocab/entries/{id}/merge/{targetId}");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "Merges the entry into the target entry (same track): keys and the meanings the target lacks move over, both get a merge row, the source is deleted. The card side comes in 0022.8b.");
    }

    public override async Task HandleAsync(MergeVocabEntryRequest req, CancellationToken ct)
    {
        await SendOutcomeAsync(await _editor.MergeAsync(req.Id, req.TargetId, UserId, ct), ct);
    }
}

public sealed class Validator : Validator<MergeVocabEntryRequest>
{
    public Validator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.TargetId).GreaterThan(0).NotEqual(x => x.Id).WithMessage("An entry cannot be merged into itself.");
    }
}
