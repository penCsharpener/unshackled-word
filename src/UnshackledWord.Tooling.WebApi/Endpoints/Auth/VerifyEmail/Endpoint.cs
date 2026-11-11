using FastEndpoints;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Domain.WebApi.Auth;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Auth.VerifyEmail;

public sealed class Endpoint : Endpoint<TokenRequest>
{
    private readonly IAuthRepository _repository;

    public Endpoint(IAuthRepository repository)
    {
        _repository = repository;
    }

    public override void Configure()
    {
        Post("verify-email");
        Group<RouteGroupConfig>();
        AllowAnonymous();
        Validator<VerifyEmailValidator>(); // the request type is shared with another endpoint
        Throttle(hitLimit: 10, durationSeconds: 60, headerName: null);
        Description(b => b.Produces(204).Produces(404));
        Summary(s => s.Summary = "Consumes an email-verification token. 404 if invalid, used or expired.");
    }

    public override async Task HandleAsync(TokenRequest req, CancellationToken ct)
    {
        var userId = await _repository.ConsumeEmailVerificationAsync(AuthTokenHasher.Hash(req.Token), ct);

        if (userId is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.NoContentAsync(ct);
    }
}

public sealed class VerifyEmailValidator : Validator<TokenRequest>
{
    public VerifyEmailValidator()
    {
        Include(new TokenRequestValidator());
    }
}
