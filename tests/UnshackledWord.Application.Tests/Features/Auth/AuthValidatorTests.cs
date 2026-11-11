using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Domain.WebApi.Auth;

namespace UnshackledWord.Application.Tests.Features.Auth;

public class AuthValidatorTests
{
    private static RegisterRequest ValidRegister() => new()
    {
        Email = "reader@example.com",
        Password = "correct horse",
        DisplayName = "Reader",
    };

    [Fact]
    public void Register_ValidRequest_Passes()
    {
        new RegisterRequestValidator().Validate(ValidRegister()).IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    public void Register_BadEmail_Fails(string email)
    {
        var request = ValidRegister();
        request.Email = email;

        new RegisterRequestValidator().Validate(request).IsValid.ShouldBeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    public void Register_WeakPassword_Fails(string password)
    {
        var request = ValidRegister();
        request.Password = password;

        new RegisterRequestValidator().Validate(request).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Register_EmptyDisplayName_Fails()
    {
        var request = ValidRegister();
        request.DisplayName = "";

        new RegisterRequestValidator().Validate(request).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Register_TooLongInviteCode_Fails()
    {
        var request = ValidRegister();
        request.InviteCode = new string('A', 65);

        new RegisterRequestValidator().Validate(request).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Login_RequiresEmailAndPassword()
    {
        var validator = new LoginRequestValidator();

        validator.Validate(new LoginRequest { Email = "a@b.de", Password = "x" }).IsValid.ShouldBeTrue();
        validator.Validate(new LoginRequest { Email = "", Password = "x" }).IsValid.ShouldBeFalse();
        validator.Validate(new LoginRequest { Email = "a@b.de", Password = "" }).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Token_MustNotBeEmpty()
    {
        var validator = new TokenRequestValidator();

        validator.Validate(new TokenRequest { Token = "abc" }).IsValid.ShouldBeTrue();
        validator.Validate(new TokenRequest { Token = "" }).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Email_MustBeAnAddress()
    {
        var validator = new EmailRequestValidator();

        validator.Validate(new EmailRequest { Email = "a@b.de" }).IsValid.ShouldBeTrue();
        validator.Validate(new EmailRequest { Email = "nope" }).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void ResetPassword_NeedsTokenAndStrongEnoughPassword()
    {
        var validator = new ResetPasswordRequestValidator();

        validator.Validate(new ResetPasswordRequest { Token = "t", NewPassword = "longenough" }).IsValid.ShouldBeTrue();
        validator.Validate(new ResetPasswordRequest { Token = "", NewPassword = "longenough" }).IsValid.ShouldBeFalse();
        validator.Validate(new ResetPasswordRequest { Token = "t", NewPassword = "short" }).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void PasskeyLoginOptions_EmailIsOptionalButValidatedWhenGiven()
    {
        var validator = new GetPasskeyLoginOptionsRequestValidator();

        validator.Validate(new GetPasskeyLoginOptionsRequest()).IsValid.ShouldBeTrue();
        validator.Validate(new GetPasskeyLoginOptionsRequest { Email = "a@b.de" }).IsValid.ShouldBeTrue();
        validator.Validate(new GetPasskeyLoginOptionsRequest { Email = "nope" }).IsValid.ShouldBeFalse();
    }
}
