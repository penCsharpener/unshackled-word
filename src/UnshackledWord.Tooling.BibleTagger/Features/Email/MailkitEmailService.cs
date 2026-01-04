using Microsoft.AspNetCore.Identity;
using UnshackledWord.Tooling.BibleTagger.Data;

namespace UnshackledWord.Tooling.BibleTagger.Features.Email;

public class MailkitEmailService : IEmailSender<ApplicationUser>
{
    private readonly IEmailService _emailService;

    public MailkitEmailService(IEmailService emailService)
    {
        _emailService = emailService;
    }

    public async Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink)
    {
        var mailText = $"""
                        Hello {user.UserName},

                        Thanks for registering at BibleTagger. Please confirm your new account by clicking this link:

                        {confirmationLink.Replace("&amp;", "&")}

                        Thank you for your contributions to this project to provide free Bible data to the global church.
                        """;

        await _emailService.SendAsync(user.UserName!, user.Email!, "BibleTagger Confirmation Link", mailText, false);
    }

    public async Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink)
    {
        var mailText = $"""
                        Hello {user.UserName},

                        Did you forget your password? If you trigger a password reset request then follow the link:

                        {resetLink.Replace("&amp;", "&")}

                        Otherwise ignore this email.
                        """;

        await _emailService.SendAsync(user.UserName!, user.Email!, "BibleTagger Password Reset Link", mailText, false);
    }

    public async Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode)
    {
        var mailText = $"""
                        Hello {user.UserName},

                        Did you forget your password? Below is your password reset code:

                        {resetCode}
                        """;

        await _emailService.SendAsync(user.UserName!, user.Email!, "BibleTagger Password Reset Code", mailText, false);
    }
}
