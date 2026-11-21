using FastEndpoints;
using FluentValidation;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Infrastructure.Vocab;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.VocabEditor.ListEntries;

/// <summary>The editor list (roadmap 0022.4): entries with filters for unreviewed, no audio, form missing, form from agent, several keys, retired, a rank range, a course and a search text.</summary>
[RequiresLogin]
public sealed class Endpoint : Endpoint<ListVocabEditorEntriesRequest, ListVocabEditorEntriesResponse>
{
    private readonly VocabEditorRepository _repository;

    public Endpoint(VocabEditorRepository repository)
    {
        _repository = repository;
    }

    public override void Configure()
    {
        Get("vocab/entries");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "Vocab entries for the editor list in rank order (take at most 200). Filters combine with AND: unreviewed, noAudio, formMissing, formFromAgent, multiKey, retired, rankFrom/rankTo, course, q. Each row carries the learner count. 404 for an unknown course.");
    }

    public override async Task HandleAsync(ListVocabEditorEntriesRequest req, CancellationToken ct)
    {
        var result = await _repository.ListAsync(req);

        if (result is null)
        {
            await Send.NotFoundAsync(ct);

            return;
        }

        await Send.OkAsync(result, ct);
    }
}

public sealed class Validator : Validator<ListVocabEditorEntriesRequest>
{
    public Validator()
    {
        RuleFor(x => x.Skip).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Take).InclusiveBetween(1, ListVocabEditorEntriesRequest.MaxTake);
        RuleFor(x => x.Q).MaximumLength(ListVocabEditorEntriesRequest.MaxQueryLength);
        RuleFor(x => x.Course).MaximumLength(30);
        RuleFor(x => x.RankFrom).GreaterThanOrEqualTo(1).When(x => x.RankFrom is not null);
        RuleFor(x => x.RankTo).GreaterThanOrEqualTo(1).When(x => x.RankTo is not null);
        RuleFor(x => x).Must(x => x.RankFrom is null || x.RankTo is null || x.RankFrom <= x.RankTo).WithMessage("rankFrom must not exceed rankTo.");
    }
}
