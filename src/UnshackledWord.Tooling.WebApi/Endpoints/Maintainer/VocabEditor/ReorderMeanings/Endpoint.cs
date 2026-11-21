using FastEndpoints;
using FluentValidation;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Infrastructure.Vocab;
using UnshackledWord.Tooling.WebApi.Auth;
using UnshackledWord.Application.Features.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.VocabEditor.ReorderMeanings;

/// <summary>Renumbers the meanings 1..n in the given order (every meaning id of the entry exactly once). Bumps nothing.</summary>
[RequiresLogin]
public sealed class Endpoint : VocabEditEndpoint<ReorderVocabMeaningsRequest>
{
    private readonly VocabEntryEditor _editor;

    public Endpoint(VocabEntryEditor editor)
    {
        _editor = editor;
    }

    public override void Configure()
    {
        Put("vocab/entries/{id}/meanings/order");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "Renumbers the meanings 1..n in the given order (every meaning id of the entry exactly once). Bumps nothing.");
    }

    public override async Task HandleAsync(ReorderVocabMeaningsRequest req, CancellationToken ct)
    {
        await SendOutcomeAsync(await _editor.ReorderMeaningsAsync(req.Id, req.MeaningIds, UserId, ct), ct);
    }
}

public sealed class Validator : Validator<ReorderVocabMeaningsRequest>
{
    public Validator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.MeaningIds).NotEmpty().Must(ids => ids.Distinct().Count() == ids.Count).WithMessage("meaningIds must not repeat an id.");
    }
}
