using FastEndpoints;
using UnshackledWord.Domain.WebApi.Vocab;
using UnshackledWord.Infrastructure.Vocab;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Vocab.GetChanges;

/// <summary>The user's cards whose word was revised or removed since they last accepted it (roadmap 0022.8b). Login required.</summary>
[RequiresLogin]
public sealed class Endpoint : VocabSessionEndpoint<EmptyRequest, VocabChangesResponse>
{
    private readonly VocabChangeService _service;

    public Endpoint(VocabChangeService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Get("changes");
        Group<RouteGroupConfig>();
        Summary(s => s.Summary = "Flagged cards: per word the status (changed or removed) and the change rows newer than the card's seen version (kind, before, after, time, merge/split target).");
    }

    public override async Task HandleAsync(EmptyRequest req, CancellationToken ct)
    {
        await SendOutcomeAsync(await _service.ListAsync(UserId, ct), ct);
    }
}
