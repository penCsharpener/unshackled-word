using Microsoft.Extensions.Options;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Tests;

public class AuthEmailBuilderTests
{
    private static AuthEmailBuilder Builder() => new(Options.Create(new AppOptions()));

    [Fact]
    public void PasswordReset_German_SaysOnlyTheNewestLinkIsValid()
    {
        var message = Builder().BuildPasswordReset("a@example.org", "Anna", "TOKEN", "de");

        message.PlainTextBody.ShouldContain("nur der neueste Link ist gültig");
    }

    [Fact]
    public void PasswordReset_English_SaysOnlyTheNewestLinkIsValid()
    {
        var message = Builder().BuildPasswordReset("a@example.org", "Anna", "TOKEN", "en");

        message.PlainTextBody.ShouldContain("only the newest link is valid");
    }

    [Fact]
    public void Welcome_LinksToTheResetPageInWelcomeMode_WithSetPasswordText()
    {
        var message = Builder().BuildWelcome("a@example.org", "Anna", "TOK EN", "de");

        message.Subject.ShouldBe("Willkommen bei Unshackled Word \u2013 Passwort festlegen");
        message.PlainTextBody.ShouldContain("http://localhost:5173/reset-password?token=TOK%20EN&welcome=1");
        message.PlainTextBody.ShouldContain("72 Stunden");
        message.HtmlBody.ShouldContain("Passwort festlegen");
    }

    [Fact]
    public void EmailChangedNotice_NamesOldAndNewAddress_ForBothRecipients()
    {
        var toOld = Builder().BuildEmailChangedNotice("old@example.org", "Anna", "old@example.org", "new@example.org", false, "de");
        var toNew = Builder().BuildEmailChangedNotice("new@example.org", "Anna", "old@example.org", "new@example.org", true, "de");

        toOld.PlainTextBody.ShouldContain("Deine E-Mail-Adresse wurde von einem Administrator ge\u00e4ndert auf new@example.org");
        toNew.PlainTextBody.ShouldContain("Dein Konto verwendet jetzt diese Adresse (new@example.org)");
        toNew.ToAddress.ShouldBe("new@example.org");
    }
}
