using Microsoft.AspNetCore.Identity;
using UnshackledWord.Tooling.BibleTagger.Data;

namespace UnshackledWord.Tooling.BibleTagger.Features.Email;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddEmail(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IEmailSender<ApplicationUser>, MailkitEmailService>();
        services.AddScoped<IEmailService, EmailService>();

        return services;
    }
}
