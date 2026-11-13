using Microsoft.Extensions.DependencyInjection;
using UnshackledWord.Infrastructure.Seed.Mappings;
using UnshackledWord.Infrastructure.Seed.Morphology;

namespace UnshackledWord.Infrastructure.Seed;

public static class ServiceCollectionExtensions
{
    /// <summary>The seed exporters (read-only) and the manifest store; used by the SeedDb CLI and by the Web API's maintainer download.</summary>
    public static IServiceCollection AddSeedExportServices(this IServiceCollection services)
    {
        services.AddScoped<SeedManifestStore>();
        services.AddScoped<MorphologyExportRepository>();
        services.AddScoped<MorphologyExporter>();
        services.AddScoped<MappingExportRepository>();
        services.AddScoped<MappingExporter>();
        services.AddScoped<SeedExportRunner>();

        return services;
    }
}
