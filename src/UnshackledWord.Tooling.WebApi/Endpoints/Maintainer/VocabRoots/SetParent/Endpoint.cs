using System.Security.Claims;
using FastEndpoints;
using FluentValidation;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Domain.WebApi.Vocab;
using UnshackledWord.Infrastructure.Vocab;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.VocabRoots.SetParent;

/// <summary>Sets or clears the parent of a root (roadmap 0030.5): same track, no cycle, at most 4 levels of parents. Returns the root with its new family card.</summary>
[RequiresLogin]
public sealed class Endpoint : Endpoint<SetVocabRootParentRequest, VocabRootDetailResponse>
{
    private readonly VocabRootEditor _editor;
    private readonly VocabRootRepository _repository;

    public Endpoint(VocabRootEditor editor, VocabRootRepository repository)
    {
        _editor = editor;
        _repository = repository;
    }

    public override void Configure()
    {
        Put("vocab/roots/{id}/parent");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "Sets the parent root (parentRootId, or for a Greek word without a root row parentStrongsKey) or clears it (neither). Refuses a parent of the other track, itself, a cycle (the message names the path) and a chain longer than 4 levels. Returns the root's family card.");
    }

    public override async Task HandleAsync(SetVocabRootParentRequest req, CancellationToken ct)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var outcome = await _editor.SetParentAsync(req.Id, req.ParentRootId, req.ParentStrongsKey, userId, ct);

        if (outcome.Status != VocabEditStatus.Ok)
        {
            AddError(outcome.Message ?? "The parent was refused.");
            await Send.ErrorsAsync(outcome.Status == VocabEditStatus.NotFound ? StatusCodes.Status404NotFound : StatusCodes.Status400BadRequest, ct);

            return;
        }

        await Send.OkAsync((await _repository.GetDetailAsync(req.Id))!, ct);
    }
}

public sealed class Validator : Validator<SetVocabRootParentRequest>
{
    public Validator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.ParentRootId).GreaterThan(0).When(x => x.ParentRootId is not null);
        RuleFor(x => x.ParentStrongsKey).Matches(@"^[Gg]\d{1,5}[A-Za-z]?$").WithMessage("parentStrongsKey must look like G3140.").When(x => !string.IsNullOrWhiteSpace(x.ParentStrongsKey));
    }
}
