using FastEndpoints;
using FluentValidation;
using UnshackledWord.Domain.WebApi.Vocab;
using UnshackledWord.Infrastructure.Vocab;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Vocab.ApplyChange;

/// <summary>Keeps the card of a revised word (keeps its progress) or resets it (deletes the card, the word comes back as new) (roadmap 0022.8b). Login required.</summary>
[RequiresLogin]
public sealed class Endpoint : VocabSessionEndpoint<VocabChangeActionRequest, VocabChangeActionResponse>
{
    private readonly VocabChangeService _service;

    public Endpoint(VocabChangeService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Post("changes/{entryId}");
        Group<RouteGroupConfig>();
        Summary(s => s.Summary = "`{action: keep | reset}` for one flagged card. A removed word (retired or deleted) can only be reset (409 for keep).");
    }

    public override async Task HandleAsync(VocabChangeActionRequest req, CancellationToken ct)
    {
        await SendOutcomeAsync(await _service.ApplyAsync(UserId, req.EntryId, req.Action, ct), ct);
    }
}

public sealed class Validator : Validator<VocabChangeActionRequest>
{
    public Validator()
    {
        RuleFor(x => x.EntryId).GreaterThan(0);
        RuleFor(x => x.Action).Must(a => a is VocabChangeActionRequest.Keep or VocabChangeActionRequest.Reset).WithMessage("Action must be keep or reset.");
    }
}
