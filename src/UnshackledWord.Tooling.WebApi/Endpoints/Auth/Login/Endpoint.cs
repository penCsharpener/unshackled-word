using FastEndpoints;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Domain.WebApi.Auth;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Auth.Login;

public sealed class Endpoint : Endpoint<LoginRequest, AuthUserResponse>
{
    private readonly IAuthRepository _repository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly SessionSignIn _sessionSignIn;

    public Endpoint(IAuthRepository repository, IPasswordHasher passwordHasher, SessionSignIn sessionSignIn)
    {
        _repository = repository;
        _passwordHasher = passwordHasher;
        _sessionSignIn = sessionSignIn;
    }

    public override void Configure()
    {
        Post("login");
        Group<RouteGroupConfig>();
        AllowAnonymous();
        // Brute-force brake on the one endpoint that checks a password.
        Throttle(hitLimit: 10, durationSeconds: 60, headerName: null);
        Description(b => b.Produces<AuthUserResponse>(200).Produces(401));
        Summary(s => s.Description = "Returns 401 for wrong email, wrong password, no password set yet, or an unverified email: always the same generic response.");
    }

    public override async Task HandleAsync(LoginRequest req, CancellationToken ct)
    {
        var user = await _repository.FindByEmailAsync(EmailNormalizer.Normalize(req.Email), ct);

        if (user is null
            || user.PasswordHash is null
            || user.EmailVerifiedAt is null
            || !_passwordHasher.Verify(req.Password, user.PasswordHash))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        await _sessionSignIn.SignInAsync(HttpContext, user.Id, user.Email, user.DisplayName, user.SecurityStamp, user.Roles);
        await Send.OkAsync(new AuthUserResponse { UserId = user.Id, Email = user.Email, DisplayName = user.DisplayName }, ct);
    }
}

public sealed class LoginValidator : Validator<LoginRequest>
{
    public LoginValidator()
    {
        Include(new LoginRequestValidator());
    }
}
