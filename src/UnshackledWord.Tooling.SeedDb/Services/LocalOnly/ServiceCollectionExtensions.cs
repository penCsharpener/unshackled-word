using UnshackledWord.Tooling.SeedDb.Services.LocalOnly.Repositories;

namespace UnshackledWord.Tooling.SeedDb.Services.LocalOnly;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddLocalOnlyServices(this IServiceCollection services)
    {
        services.AddScoped<CopyBackMappingRepository>();
        services.AddScoped<LocalOnlyRunner>();
        return services;
    }


}
