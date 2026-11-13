using UnshackledWord.Infrastructure.Seed;
using UnshackledWord.Tooling.SeedDb.Services.RepoSeed.Mappings;
using UnshackledWord.Tooling.SeedDb.Services.RepoSeed.Morphology;
using UnshackledWord.Tooling.SeedDb.Services.RepoSeed.Verbs;

namespace UnshackledWord.Tooling.SeedDb.Services.RepoSeed;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRepoSeedServices(this IServiceCollection services)
    {
        services.AddSeedExportServices();
        services.AddScoped<ElbWordTokenReader>();
        services.AddScoped<MorphologySeedRepository>();
        services.AddScoped<MorphologySeedRunner>();
        services.AddScoped<MappingSeedRepository>();
        services.AddScoped<MappingSeedSourceReader>();
        services.AddScoped<MappingSeedImporter>();
        services.AddScoped<ISeedVerb, ExportSeedVerb>();
        services.AddScoped<RebaseRepository>();
        services.AddScoped<ISeedVerb, RebaseVerb>();
        services.AddScoped<ISeedVerb, AnchorCheckVerb>();

        return services;
    }
}
