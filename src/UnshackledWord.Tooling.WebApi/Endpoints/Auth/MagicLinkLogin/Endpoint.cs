using FastEndpoints;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Domain.WebApi.Auth;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Auth.MagicLinkLogin;

public sealed class Endpoint : Endpoint<TokenRequest, AuthUserResponse>
{
    private readonly IAuthRepository _repository;
    private readonly SessionSignIn _sessionSignIn;

    public Endpoint(IAuthRepository repository, SessionSignIn sessionSignIn)
    {
        _repository = repository;
        _sessionSignIn = sessionSignIn;
    }

    public override void Configure()
    {
        Post("magic-link-login");
        Group<RouteGroupConfig>();
        AllowAnonymous();
        Validator<MagicLinkLoginValidator>(); // the request type is shared with another endpoint
        Throttle(hitLimit: 10, durationSeconds: 60, headerName: null);
        Description(b => b.Produces<AuthUserResponse>(200).Produces(404));
        Summary(s => s.Summary = "Consumes a magic-link token and signs in. 404 if invalid, used or expired.");
    }

    public override async Task HandleAsync(TokenRequest req, CancellationToken ct)
    {
        var user = await _repository.ConsumeMagicLinkAsync(AuthTokenHasher.Hash(req.Token), ct);

        if (user is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await _sessionSignIn.SignInAsync(HttpContext, user.Id, user.Email, user.DisplayName, user.SecurityStamp, user.Roles);
        await Send.OkAsync(new AuthUserResponse { UserId = user.Id, Email = user.Email, DisplayName = user.DisplayName }, ct);
    }
}

public sealed class MagicLinkLoginValidator : Validator<TokenRequest>
{
    public MagicLinkLoginValidator()
    {
        Include(new TokenRequestValidator());
    }
}
