using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using UnshackledWord.Tooling.BibleTagger.Components.Account;
using UnshackledWord.Tooling.BibleTagger.Data;
using UnshackledWord.Tooling.BibleTagger.Features.Configuration;
using UnshackledWord.Tooling.BibleTagger.Features.Elb1871SrTaggerRepository;
using UnshackledWord.Tooling.BibleTagger.Features.Email;

namespace UnshackledWord.Tooling.BibleTagger.Features.ApplicationSetup;

public static class Extensions
{
    public static WebApplicationBuilder ApplicationSetup(this WebApplicationBuilder builder)
    {
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents()
            .AddCircuitOptions(x => x.DetailedErrors = builder.Environment.IsDevelopment());
        builder.Services.AddDbContentAndIdentity(builder.Configuration);
        builder.Services.AddAppSettings(builder.Configuration);
        builder.Services.AddEmail(builder.Configuration);
        builder.Services.AddElb1871Tagging();

        return builder;
    }

    public static IServiceCollection AddDbContentAndIdentity(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddCascadingAuthenticationState();
        services.AddScoped<IdentityRedirectManager>();
        services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

        services.AddAuthentication(options =>
            {
                options.DefaultScheme = IdentityConstants.ApplicationScheme;
                options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
            })
            .AddIdentityCookies();

        var connectionString = configuration.GetConnectionString("DefaultConnection") ??
                               throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionString));
        services.AddDatabaseDeveloperPageExceptionFilter();

        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.SignIn.RequireConfirmedAccount = true;
                options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        return services;
    }

    public static HttpClient GetCoreApiClient(this IHttpClientFactory clientFactory)
    {
        return clientFactory.CreateClient("core-api");
    }
}
