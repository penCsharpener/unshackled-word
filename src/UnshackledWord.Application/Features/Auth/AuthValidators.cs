using FluentValidation;
using UnshackledWord.Domain.WebApi.Auth;

namespace UnshackledWord.Application.Features.Auth;

// The rules live here (not in the endpoint classes) so they can be unit-tested; the endpoints include them.

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8).MaximumLength(200);
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.InviteCode).MaximumLength(64);
    }
}

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty();
        RuleFor(x => x.Password).NotEmpty();
    }
}

/// <summary>For verify-email and magic-link-login.</summary>
public sealed class TokenRequestValidator : AbstractValidator<TokenRequest>
{
    public TokenRequestValidator()
    {
        RuleFor(x => x.Token).NotEmpty();
    }
}

/// <summary>For request-password-reset and request-magic-link.</summary>
public sealed class EmailRequestValidator : AbstractValidator<EmailRequest>
{
    public EmailRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(320);
    }
}

public sealed class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(x => x.Token).NotEmpty();
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(8).MaximumLength(200);
    }
}

public sealed class GetPasskeyLoginOptionsRequestValidator : AbstractValidator<GetPasskeyLoginOptionsRequest>
{
    public GetPasskeyLoginOptionsRequestValidator()
    {
        RuleFor(x => x.Email).EmailAddress().MaximumLength(320).When(x => !string.IsNullOrEmpty(x.Email));
    }
}
