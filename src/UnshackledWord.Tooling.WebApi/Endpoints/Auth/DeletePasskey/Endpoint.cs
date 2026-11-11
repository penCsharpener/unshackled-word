using System.Security.Claims;
using FastEndpoints;
using UnshackledWord.Domain.WebApi.Auth;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Auth.DeletePasskey;

[RequiresLogin]
public sealed class Endpoint : Endpoint<DeletePasskeyRequest>
{
    private readonly IAuthRepository _repository;

    public Endpoint(IAuthRepository repository)
    {
        _repository = repository;
    }

    public override void Configure()
    {
        Delete("passkeys/{id}");
        Group<RouteGroupConfig>();
        Description(b => b.Produces(204).Produces(404));
        Summary(s =>
        {
            s.Summary = "Deletes one of the caller's own passkeys.";
            s[404] = "No such passkey, or it belongs to someone else.";
        });
    }

    public override async Task HandleAsync(DeletePasskeyRequest req, CancellationToken ct)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        if (await _repository.DeletePasskeyAsync(req.Id, userId, ct))
        {
            await Send.NoContentAsync(ct);
            return;
        }

        await Send.NotFoundAsync(ct);
    }
}
