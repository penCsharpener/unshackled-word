using System.Security.Claims;
using FastEndpoints;
using UnshackledWord.Domain.WebApi.Vocab;
using UnshackledWord.Infrastructure.Vocab;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Vocab.GetSettings;

/// <summary>The caller's vocab settings: disabled quiz types and the types that can be switched (roadmap 0022.9). Login required.</summary>
[RequiresLogin]
public sealed class Endpoint : Endpoint<EmptyRequest, VocabSettingsResponse>
{
    private readonly VocabSettingsService _service;

    public Endpoint(VocabSettingsService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Get("settings");
        Group<RouteGroupConfig>();
        Summary(s => s.Summary = "The learner's vocab settings: `disabledQuizTypes` (turned off) and `availableQuizTypes` (switchable now).");
    }

    public override async Task HandleAsync(EmptyRequest req, CancellationToken ct)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await Send.OkAsync(await _service.GetAsync(userId, ct), ct);
    }
}
