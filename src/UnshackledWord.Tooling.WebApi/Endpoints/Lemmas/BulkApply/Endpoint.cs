using System.Security.Claims;
using FastEndpoints;
using FluentValidation;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Domain.Models.Grammar;
using UnshackledWord.Domain.WebApi.Lemmas;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Lemmas.BulkApply;

[RequiresLogin]
public sealed class Endpoint : Endpoint<BulkApplyRequest>
{
    private readonly ILemmaBulkRepository _repository;

    public Endpoint(ILemmaBulkRepository repository)
    {
        _repository = repository;
    }

    public override void Configure()
    {
        Post("bulk/apply");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "Replaces the lemma of the listed words (all-or-nothing). 409 {stale:[wordIds]} when a row no longer has one of the source lemmas.");
    }

    public override async Task HandleAsync(BulkApplyRequest req, CancellationToken ct)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var (stale, changeSetId, updated) = await _repository.ApplyAsync(req, userId, ct);
        if (stale.Count > 0)
        {
            await Send.ResponseAsync(new BulkStaleResponse { Stale = stale.Select(d => d.WordId).ToList(), Details = stale }, 409, ct);
            return;
        }

        await Send.OkAsync(new BulkApplyResponse { ChangeSetId = changeSetId, Updated = updated }, ct);
    }
}

public sealed class Validator : Validator<BulkApplyRequest>
{
    public Validator()
    {
        RuleFor(x => x.From).ValidFromLemmas();
        RuleFor(x => x.To).Must(LemmaBulkRules.IsValidLemma).WithMessage("to must be 1-255 characters (trimmed) without control characters.");
        RuleFor(x => x).Must(x => !LemmaBulkRules.NormalizeFrom(x.From).Contains(LemmaBulkRules.Normalize(x.To), StringComparer.Ordinal))
            .WithName("to").WithMessage("to must not equal one of the source lemmas.");
        RuleFor(x => x.WordIds).NotEmpty()
            .Must(ids => ids.Length <= LemmaBulkRules.MaxApplyWords).WithMessage($"At most {LemmaBulkRules.MaxApplyWords} word ids.")
            .Must(ids => ids.All(i => i > 0)).WithMessage("wordIds must be positive.")
            .Must(ids => ids.Distinct().Count() == ids.Length).WithMessage("wordIds must be distinct.");
        RuleFor(x => x.ExpectedOriginal).NotNull()
            .Must((x, expected) => expected.Length == x.WordIds.Length).WithMessage("expectedOriginal must have the same length as wordIds.")
            .Must(expected => expected.All(OriginalLanguageRules.IsValidExpectedOriginal)).WithMessage("expectedOriginal entries must be \"\", added, none or a G/H Strong's key.");
        RuleFor(x => x.Note).MaximumLength(LemmaBulkRules.MaxNoteLength);
        RuleFor(x => x.Stts).Must(c => ElbLemmaValueMaps.IsValid(ElbLemmaValueMaps.Stts, c)).When(x => x.Stts is not null).WithMessage("Unknown stts code.");
    }
}
