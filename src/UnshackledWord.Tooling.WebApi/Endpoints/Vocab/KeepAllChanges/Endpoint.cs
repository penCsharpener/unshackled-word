using FastEndpoints;
using UnshackledWord.Domain.WebApi.Vocab;
using UnshackledWord.Infrastructure.Vocab;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Vocab.KeepAllChanges;

/// <summary>"Keep all": accepts every revised word at once; removed words stay in the list (roadmap 0022.8b). Login required.</summary>
[RequiresLogin]
public sealed class Endpoint : VocabSessionEndpoint<EmptyRequest, VocabKeepAllResponse>
{
    private readonly VocabChangeService _service;

    public Endpoint(VocabChangeService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Post("changes/keep-all");
        Group<RouteGroupConfig>();
        Summary(s => s.Summary = "Keeps every flagged card whose word still exists; retired and deleted words stay (they can only be removed one by one).");
    }

    public override async Task HandleAsync(EmptyRequest req, CancellationToken ct)
    {
        await SendOutcomeAsync(await _service.KeepAllAsync(UserId, ct), ct);
    }
}
