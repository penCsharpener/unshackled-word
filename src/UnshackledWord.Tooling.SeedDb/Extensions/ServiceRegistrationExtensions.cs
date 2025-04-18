using UnshackledWord.Domain.Models.Settings;
using UnshackledWord.Infrastructure.Extensions;
using UnshackledWord.Tooling.SeedDb.Services;

namespace UnshackledWord.Tooling.SeedDb.Extensions;

public static class ServiceRegistrationExtensions
{
    public static void AddSeedDbServices(this HostApplicationBuilder builder)
    {
        builder.Services.AddInfrastructureServices();
        builder.Services.AddScoped<SeedingService>();
        builder.Services.Configure<AppSettings>(builder.Configuration.GetSection(nameof(AppSettings)));
    }
}
