using System.Security.Claims;
using FastEndpoints;
using UnshackledWord.Domain.WebApi.Auth;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Auth.ListPasskeys;

[RequiresLogin]
public sealed class Endpoint : EndpointWithoutRequest<ListPasskeysResponse>
{
    private readonly IAuthRepository _repository;

    public Endpoint(IAuthRepository repository)
    {
        _repository = repository;
    }

    public override void Configure()
    {
        Get("passkeys");
        Group<RouteGroupConfig>();
        Summary(s => s.Summary = "Lists the caller's own registered passkeys.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var rows = await _repository.ListPasskeysAsync(userId, ct);

        await Send.OkAsync(new ListPasskeysResponse
        {
            Passkeys = rows.Select(r => new PasskeySummary
            {
                Id = r.Id,
                Name = r.Name,
                CreatedAt = r.CreatedAt,
                LastUsedAt = r.LastUsedAt,
            }).ToList(),
        }, ct);
    }
}
