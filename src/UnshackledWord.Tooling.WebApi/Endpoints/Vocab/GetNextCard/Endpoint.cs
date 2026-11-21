using FastEndpoints;
using FluentValidation;
using UnshackledWord.Domain.WebApi.Vocab;
using UnshackledWord.Infrastructure.Vocab;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Vocab.GetNextCard;

/// <summary>The next card of the caller's own session (roadmap 0022.8). Repeated calls return the same card until it is answered. Login required.</summary>
[RequiresLogin]
public sealed class Endpoint : VocabSessionEndpoint<GetVocabSessionNextRequest, VocabNextResponse>
{
    private readonly VocabSessionService _service;

    public Endpoint(VocabSessionService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Get("sessions/{id}/next");
        Group<RouteGroupConfig>();
        Summary(s => s.Summary = "The next card: an introduction (meanings, one verse) or a multiple-choice question (four German options, never marked). Idempotent until answered. `finished: true` with the summary when the session is done. 404 for a session of another user.");
    }

    public override async Task HandleAsync(GetVocabSessionNextRequest req, CancellationToken ct)
    {
        await SendOutcomeAsync(await _service.NextAsync(UserId, req.Id, ct), ct);
    }
}

public sealed class Validator : Validator<GetVocabSessionNextRequest>
{
    public Validator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
    }
}
