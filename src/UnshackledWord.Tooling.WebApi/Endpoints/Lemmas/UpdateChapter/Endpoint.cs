using System.Security.Claims;
using FastEndpoints;
using FluentValidation;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Domain.WebApi.Lemmas;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Lemmas.UpdateChapter;

[RequiresLogin]
public sealed class Endpoint : Endpoint<UpdateLemmaChapterRequest, UpdateLemmaChapterResponse>
{
    private readonly ILemmaRepository _repository;

    public Endpoint(ILemmaRepository repository)
    {
        _repository = repository;
    }

    public override void Configure()
    {
        Put("chapter");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "Batch variant of PUT words/{wordId}: all changes in one transaction (all or nothing).");
    }

    public override async Task HandleAsync(UpdateLemmaChapterRequest req, CancellationToken ct)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var missing = await _repository.ApplyAsync(req.Changes, userId, ct);
        if (missing.Count > 0)
        {
            AddError($"Unknown word ids: {string.Join(", ", missing)}");
            await Send.ErrorsAsync(404, ct);
            return;
        }

        var rows = await _repository.GetWordsAsync(req.Changes.Select(c => c.WordId).ToList(), ct);
        await Send.OkAsync(new UpdateLemmaChapterResponse { Updated = req.Changes.Count, Words = rows }, ct);
    }
}

public sealed class Validator : Validator<UpdateLemmaChapterRequest>
{
    public const int MaxChanges = 2000;

    public Validator()
    {
        RuleFor(x => x.Changes).NotEmpty().Must(c => c.Count <= MaxChanges).WithMessage($"At most {MaxChanges} changes per request.");
        RuleFor(x => x.Changes).Must(c => c.Select(x => x.WordId).Distinct().Count() == c.Count).WithMessage("Duplicate wordId in changes.");
        RuleForEach(x => x.Changes).SetValidator(new ChangeValidator());
    }
}

public sealed class ChangeValidator : AbstractValidator<LemmaWordChange>
{
    public ChangeValidator()
    {
        Include(new LemmaEditFieldsValidator());
        RuleFor(x => x.WordId).GreaterThan(0);
    }
}
