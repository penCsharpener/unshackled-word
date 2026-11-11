using System.Security.Claims;
using Fido2NetLib;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using UnshackledWord.Application.Features.Auth;

namespace UnshackledWord.Tooling.WebApi.Auth;

public static class AuthServiceCollectionExtensions
{
    public const string SessionCookieName = "unshackled_session";

    public static IServiceCollection AddAuth(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AppOptions>(configuration.GetSection("App"));
        services.Configure<EmailOptions>(configuration.GetSection("Email"));
        services.Configure<RegistrationOptions>(configuration.GetSection("Registration"));

        services.AddMemoryCache();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
        services.AddSingleton<IEmailSender, MailKitEmailSender>();
        services.AddSingleton<PasskeyCeremonyStore>();
        services.AddScoped<AuthEmailBuilder>();
        services.AddScoped<InvitationEmailBuilder>();
        services.AddScoped<IInviteAdminRepository, InviteAdminRepository>();
        services.AddScoped<IAuthRepository, AuthRepository>();
        services.AddScoped<IAdmissionRepository>(sp => sp.GetRequiredService<IAuthRepository>());
        services.AddScoped<AdmissionCheck>();
        services.AddSingleton<SessionSignIn>();

        var appOptions = configuration.GetSection("App").Get<AppOptions>() ?? new AppOptions();

        // Relying-party id and origin derive from App:FrontendBaseUrl; the browser only ever talks to the
        // frontend origin (Vite proxies /api in dev). Attestation is never requested, so no metadata service.
        var frontendUri = new Uri(appOptions.FrontendBaseUrl);
        services.AddSingleton<IFido2>(new Fido2(
            new Fido2Configuration
            {
                ServerDomain = frontendUri.Host,
                ServerName = "Unshackled Word",
                Origins = new HashSet<string> { appOptions.FrontendBaseUrl.TrimEnd('/') },
            },
            metadataService: null!));

        services
            .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.Cookie.Name = SessionCookieName;
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = appOptions.RequireSecureCookies
                    ? CookieSecurePolicy.Always
                    : CookieSecurePolicy.SameAsRequest;
                options.ExpireTimeSpan = TimeSpan.FromDays(14);
                options.SlidingExpiration = true;

                // JSON API: answer with the status code instead of redirecting to an HTML login page.
                options.Events.OnRedirectToLogin = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };
                options.Events.OnRedirectToAccessDenied = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                };

                // Reject a cookie issued before a security-relevant change (password reset): one indexed lookup
                // per authenticated request, comparing the stamp baked in at login with the current DB value.
                options.Events.OnValidatePrincipal = async context =>
                {
                    var userIdClaim = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                    var stampClaim = context.Principal?.FindFirstValue(AppClaimTypes.SecurityStamp);

                    if (userIdClaim is null || stampClaim is null)
                    {
                        context.RejectPrincipal();
                        return;
                    }

                    var repository = context.HttpContext.RequestServices.GetRequiredService<IAuthRepository>();
                    var currentStamp = await repository.GetSecurityStampAsync(
                        int.Parse(userIdClaim), context.HttpContext.RequestAborted);

                    if (currentStamp is null || currentStamp != stampClaim)
                    {
                        context.RejectPrincipal();
                        await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                    }
                };
            });

        services.AddAuthorization();
        return services;
    }
}
