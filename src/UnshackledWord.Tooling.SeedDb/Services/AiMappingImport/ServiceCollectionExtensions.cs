namespace UnshackledWord.Tooling.SeedDb.Services.AiMappingImport;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAiMappingImportServices(this IServiceCollection services)
    {
        services.AddScoped<AiMappingImportRunner>();
        services.AddScoped<AiMappingImportRunnerRepository>();
        return services;
    }
}