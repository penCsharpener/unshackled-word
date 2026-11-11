using FluentValidation;
using UnshackledWord.Domain.WebApi.Auth;

namespace UnshackledWord.Application.Features.Auth;

public sealed class CreateInviteCodeRequestValidator : AbstractValidator<CreateInviteCodeRequest>
{
    public CreateInviteCodeRequestValidator()
    {
        RuleFor(x => x.MaxSeats).GreaterThan(0);

        // Func overload: the validator instance is reused, a plain value would freeze "now" at startup.
        RuleFor(x => x.ExpiresAt)
            .GreaterThan(_ => DateTimeOffset.UtcNow)
            .WithMessage("ExpiresAt must be in the future.");

        RuleFor(x => x.Note).MaximumLength(500);
        RuleFor(x => x.BoundEmail).EmailAddress().MaximumLength(320).When(x => !string.IsNullOrWhiteSpace(x.BoundEmail));
    }
}

public sealed class InviteByEmailRequestValidator : AbstractValidator<InviteByEmailRequest>
{
    public InviteByEmailRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(x => x.Note).MaximumLength(500);
    }
}
