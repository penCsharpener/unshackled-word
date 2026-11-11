using FastEndpoints;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Domain.WebApi.Auth;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Auth.ResetPassword;

public sealed class Endpoint : Endpoint<ResetPasswordRequest>
{
    private readonly IAuthRepository _repository;
    private readonly IPasswordHasher _passwordHasher;

    public Endpoint(IAuthRepository repository, IPasswordHasher passwordHasher)
    {
        _repository = repository;
        _passwordHasher = passwordHasher;
    }

    public override void Configure()
    {
        Post("reset-password");
        Group<RouteGroupConfig>();
        AllowAnonymous();
        Throttle(hitLimit: 10, durationSeconds: 60, headerName: null);
        Description(b => b.Produces(204).Produces(404));
        Summary(s => s.Summary = "Consumes a password-reset token and sets a new password. 404 if invalid, used or expired.");
    }

    public override async Task HandleAsync(ResetPasswordRequest req, CancellationToken ct)
    {
        var userId = await _repository.ConsumePasswordResetAsync(
            AuthTokenHasher.Hash(req.Token), _passwordHasher.Hash(req.NewPassword), ct);

        if (userId is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.NoContentAsync(ct);
    }
}

public sealed class ResetPasswordValidator : Validator<ResetPasswordRequest>
{
    public ResetPasswordValidator()
    {
        Include(new ResetPasswordRequestValidator());
    }
}
